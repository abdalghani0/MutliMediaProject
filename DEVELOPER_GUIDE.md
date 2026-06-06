# دليل المطوّر — Audio Compression Studio

هذا الدليل موجَّه لزملاء الفريق الذين يريدون فهم كل التفاصيل: ما الذي يحتويه
كل ملف، ووظيفة كل دالة وحقل، وكيف تترابط القطع لتنتج التطبيق النهائي.

سنبدأ بنظرة معمارية سريعة، ثم سنشرح كل ملف على حدة، وأخيراً سنرسم تدفّق
البيانات لكل عملية مهمة.

---

## 1. نظرة معمارية سريعة (5 دقائق)

التطبيق منظّم في أربع طبقات منفصلة:

```
┌──────────────────────────────────────────────────────────────────┐
│  UI Layer                                                         │
│  ─────────                                                        │
│  Form1.Designer.cs  →  ينشئ ويرتّب كل عناصر الواجهة                │
│  Form1.cs           →  يحوي منطق التطبيق (events, orchestration)  │
└──────────────────────────────────────────────────────────────────┘
                                ↓ يستدعي
┌──────────────────────────────────────────────────────────────────┐
│  Compression Layer                                                │
│  ─────────────────                                                │
│  IAudioCompressor      ←  العقد (interface) المشترك               │
│  CompressorFactory     ←  ينشئ المرمّز المطلوب                     │
│  NonlinearQuantization │
│  DPCM                  │  أربع خوارزميات تنفّذ IAudioCompressor    │
│  DeltaModulation       │                                          │
│  AdaptiveDeltaModul.   │                                          │
│  BitReader / BitWriter ←  أدوات تحزيم البتات                       │
└──────────────────────────────────────────────────────────────────┘
                                ↓ تعتمد على
┌──────────────────────────────────────────────────────────────────┐
│  Services Layer                                                   │
│  ───────────────                                                  │
│  WavReader      →  قراءة ملفات WAV                                │
│  WavWriter      →  كتابة ملفات WAV                                │
│  AudioPlayer    →  تشغيل الصوت عبر MCI                            │
│  Resampler      →  تغيير معدل أخذ العينات                          │
│  CompressedFileFormat → قراءة وكتابة حاوية .amcx                  │
└──────────────────────────────────────────────────────────────────┘
                                ↓ تستهلك
┌──────────────────────────────────────────────────────────────────┐
│  Models Layer                                                     │
│  ─────────────                                                    │
│  AudioFile, CompressionSettings, CompressionResult,               │
│  ProgressUpdate, CompressionAlgorithm (enum)                      │
└──────────────────────────────────────────────────────────────────┘
```

**القاعدة الذهبية**: كل طبقة تعتمد فقط على الطبقات التي تحتها. لا تستورد طبقة
من طبقة أعلى منها أبداً. هذا يجعل الكود قابلاً للاختبار ولإعادة الاستخدام.

---

## 2. الملفات الموجودة وأين تجدها

```
MutliMediaProject/
│
├── Models/                              ← (POCOs) كائنات نقل بيانات بسيطة
│   ├── AudioFile.cs                     ← يمثّل ملف WAV محمَّل في الذاكرة
│   ├── CompressionAlgorithm.cs          ← enum للخوارزميات الأربع
│   ├── CompressionResult.cs             ← نتيجة عملية ضغط واحدة
│   ├── CompressionSettings.cs           ← الإعدادات التي يدخلها المستخدم
│   └── ProgressUpdate.cs                ← لقطة تقدم تُرسَل من المرمّز إلى الواجهة
│
├── Services/                            ← خدمات إدخال/إخراج وأدوات صوتية
│   ├── AudioPlayer.cs                   ← غلاف حول مكتبة winmm.dll للتشغيل
│   ├── CompressedFileFormat.cs          ← قراءة وكتابة ملفات .amcx
│   ├── Resampler.cs                     ← تخفيض معدل أخذ العينات
│   ├── WavReader.cs                     ← قراءة WAV → AudioFile
│   └── WavWriter.cs                     ← كتابة WAV من مصفوفة عينات
│
├── Compression/                         ← الخوارزميات وأدواتها
│   ├── IAudioCompressor.cs              ← العقد المشترك
│   ├── CompressorFactory.cs             ← ينشئ المرمّز المطلوب
│   ├── BitReader.cs                     ← قراءة بتات متغيرة العرض
│   ├── BitWriter.cs                     ← كتابة بتات متغيرة العرض
│   ├── NonlinearQuantizationCompressor.cs
│   ├── DpcmCompressor.cs
│   ├── DeltaModulationCompressor.cs
│   └── AdaptiveDeltaModulationCompressor.cs
│
├── Form1.cs                             ← منطق الواجهة (السحب، الفتح، الضغط...)
├── Form1.Designer.cs                    ← تخطيط عناصر الواجهة
├── Program.cs                           ← نقطة الدخول Main()
├── MutliMediaProject.csproj             ← إعدادات المشروع والمراجع
└── App.config                           ← إعدادات تشغيل ‎.NET
```

---

## 3. شرح كل ملف

### 3.1 طبقة الـ Models

#### `Models/CompressionAlgorithm.cs`

```csharp
public enum CompressionAlgorithm {
    NonlinearQuantization = 0,
    DifferentialPcm = 1,
    DeltaModulation = 2,
    AdaptiveDeltaModulation = 3
}
```

ترقيم صريح يضمن أن قيم الـ enum تطابق ما يُخزَّن في ملف `.amcx`. **لا تغيّروا
هذه الأرقام بعد إصدار التطبيق** وإلا توقفت الملفات القديمة عن العمل.

**`CompressionAlgorithmExtensions.ToFriendlyString()`** يحوّل القيمة إلى نص
وصفي يُعرَض في الواجهة (مثلاً "Nonlinear Quantization (\u00B5-law)").

#### `Models/AudioFile.cs`

يمثّل ملف WAV محمَّل في الذاكرة. حقوله:

| الحقل | الوصف |
|------|------|
| `FilePath` | المسار الكامل على القرص |
| `FileSizeBytes` | حجم الملف بالبايت (لحساب نسبة الضغط) |
| `SampleRate` | معدل أخذ العينات (مثلاً 44100) |
| `Channels` | 1 (mono) أو 2 (stereo) |
| `BitsPerSample` | 8 أو 16 |
| `Encoding` | دائماً "PCM" حالياً |
| `Samples` | `short[Channels][SampleCount]` — العينات بعد التحويل إلى 16 بت |

خصائص محسوبة:
- `SampleCountPerChannel` — عدد العينات في القناة الواحدة.
- `Duration` — المدة بالثواني، تُحسب من العينات ومعدّل العينات.
- `BitRate` — `SampleRate × Channels × BitsPerSample`.
- `FileName` — اسم الملف فقط (دون المسار).

**ملاحظة مهمة**: حتى لو كان الملف الأصلي 8 بت، نخزّن العينات داخلياً كـ
`short` (16 بت موقّع). هذا يبسّط كثيراً منطق الخوارزميات لأن كل شيء يعمل
بنفس النطاق `[-32768, 32767]`.

#### `Models/CompressionSettings.cs`

يحمل كل القيم التي يدخلها المستخدم في لوحة الإعدادات:

| الحقل | الخوارزميات التي تستخدمه |
|------|--------------------------|
| `Algorithm` | كلها |
| `TargetSampleRate` | كلها |
| `QuantizationBits` | NLQ, DPCM |
| `Mu` | NLQ فقط |
| `StepSize` | DM, ADM |
| `MinStepSize` / `MaxStepSize` | ADM فقط |

دالة `Clone()` تنشئ نسخة سطحية، نستخدمها عند تخزين الإعدادات في `CompressionResult`
لكيلا تتأثر بتغييرات لاحقة من المستخدم.

#### `Models/CompressionResult.cs`

نتيجة عملية ضغط واحدة. تحوي الإعدادات المستخدمة، الحجم الأصلي، الحجم بعد
الضغط، الزمن المستغرق، مسار الإخراج، وعلَم `WasCancelled`. الخواص المحسوبة:

- `SavingsPercent` = `(1 - compressed/original) × 100`
- `CompressionRatio` = `original / compressed`

تُمرَّر هذه القيم إلى دالة `WriteReport()` لطباعتها في لوحة التقرير.

#### `Models/ProgressUpdate.cs`

كائن لقطة (snapshot) يُرسَل من المرمّز إلى الواجهة كل بضعة آلاف من العينات
المعالَجة. حقوله:

- `Percent` — نسبة الإنجاز (0–100).
- `ProcessedInputBytes` / `ProducedOutputBytes` — لحساب نسبة الضغط الحية.
- `ElapsedMilliseconds` — الزمن المنقضي منذ بداية العملية.
- `Status` — نص قصير يُعرَض تحت شريط التقدم.

الخواص المحسوبة `CurrentRatio` و `SpeedMBps` تستخدمها الواجهة لإطعام
الرسوم البيانية في الزمن الحقيقي.

---

### 3.2 طبقة الـ Services

#### `Services/WavReader.cs`

```csharp
public static AudioFile Read(string path);
```

يفتح ملف WAV، يتحقق من رأس RIFF/WAVE، يقرأ كتلة `fmt ` للحصول على معدل
العينات وعدد القنوات وعمق البت، ثم يقرأ كتلة `data` للحصول على العينات
الأولية. يدعم فقط PCM (format code = 1) بـ 8 أو 16 بت، أحادي أو ستيريو —
وغير ذلك يُرفَض برسالة خطأ واضحة.

الدالة الخاصة `ConvertToInt16Samples()` تحوّل البايتات الخام إلى مصفوفة
`short[][]` بصف لكل قناة. حالات PCM 8 بت (غير الموقّعة، 0..255) تُحوَّل
إلى 16 بت موقّع `(byte - 128) << 8`.

#### `Services/WavWriter.cs`

```csharp
public static void Write(string path, short[][] samples, int sampleRate);
```

يكتب ملف PCM WAV 16 بت موقّع. يستخدمه التطبيق في حالتين:
1. حفظ نتيجة فك الضغط كملف WAV قابل للتشغيل.
2. كتابة ملف WAV مؤقّت لمعاينة الصوت المُفكوك ضغطه عبر `AudioPlayer`.

التنسيق المكتوب:
```
RIFF [4 bytes size] WAVE
fmt  [16 bytes] format=1 channels=N rate=R byteRate blockAlign bits=16
data [4 bytes size] [interleaved samples...]
```

#### `Services/AudioPlayer.cs`

يغلّف دالة Win32 `mciSendString` من `winmm.dll`. الدوال:

| الدالة | الوصف |
|--------|------|
| `Load(path)` | يفتح الملف. يحاول أولاً نوع `mpegvideo`، وإذا فشل يترك MCI يستنتج. |
| `Play()` | يبدأ التشغيل (أو يستأنف إذا كان متوقفاً مؤقتاً). |
| `Pause()` | إيقاف مؤقّت. |
| `Stop()` | إيقاف وإعادة المؤشر إلى البداية. |
| `GetPosition()` | الموضع الحالي بالـ TimeSpan. |
| `GetLength()` | المدة الكاملة للملف. |
| `Close()` | يُحرّر مورد MCI. |
| `Dispose()` | يستدعي `Close()`. |

لماذا MCI وليس مكتبة خارجية؟ لأنها متوفّرة في كل نسخ ويندوز ولا تحتاج
NuGet، وتدعم WAV ومعظم الصيغ الشائعة، وتعطي تحكماً كاملاً في التشغيل
والإيقاف والاستئناف.

#### `Services/Resampler.cs`

```csharp
public static short[] Resample(short[] input, int sourceRate, int targetRate);
```

محوّل معدل عينات بسيط بالاستيفاء الخطي (linear interpolation). لكل عينة
خرج، يحسب الموضع المقابل في الإدخال، ويستوفي خطياً بين العينتين المجاورتين.
هذا يكفي للمتطلب التعليمي للمشروع (تخفيض معدل العينات قبل الضغط)؛ ولسنا
بصدد بناء معالج إشارة عالي الجودة هنا.

#### `Services/CompressedFileFormat.cs`

يدير صيغة ملف `.amcx`. يحوي:

- ثابت `Extension = ".amcx"`.
- ثابت داخلي `Magic = "AMCX"` و `Version = 1`.
- فئة داخلية `Container` تجمّع كل ما يلزم لتخزين أو استعادة ملف مضغوط:
  `Algorithm`, `Channels`, `OriginalBitsPerSample`, `OriginalSampleRate`,
  `EncodedSampleRate`, `SamplesPerChannel`, `BitsPerCode`, `Param1`,
  `Param2`, `ChannelData[][]`.
- `Write(path, container)` يكتب الرأس ثم بيانات كل قناة مع طولها.
- `Read(path)` يعكس العملية ويتحقق من الـ magic bytes ورقم النسخة.

تخطيط البايتات مشروح في `REPORT.md` القسم 5، ويبدأ بالـ magic bytes ثم رأس
ثابت بطول 29 بايت يتبعه طول وبيانات كل قناة.

---

### 3.3 طبقة الـ Compression

#### `Compression/BitWriter.cs`

```csharp
public void WriteBits(uint value, int bitCount);
public byte[] ToArray();
```

يحزّم رموز متغيرة العرض (1 إلى 8 بت) إلى مصفوفة بايتات، MSB أولاً داخل
كل بايت. عند استدعاء `ToArray()` يُحشى البت غير المكتمل بأصفار من اليمين.

#### `Compression/BitReader.cs`

```csharp
public uint ReadBits(int bitCount);
```

عكس `BitWriter`. يقرأ بنفس الترتيب MSB-first. عند تجاوز نهاية البيانات
يُعيد أصفاراً بدلاً من رمي استثناء — هذا يجعل المرمّزات أكثر صموداً عند
انتهاء التيار قبل اكتمال آخر رمز.

#### `Compression/IAudioCompressor.cs`

العقد الذي تنفّذه كل الخوارزميات:

```csharp
public interface IAudioCompressor {
    CompressionAlgorithm Algorithm { get; }
    int GetBitsPerCode(CompressionSettings settings);
    void GetParams(CompressionSettings settings, out float p1, out float p2);
    byte[] Compress(short[] samples, CompressionSettings s,
                    IProgress<long> progress, CancellationToken token);
    short[] Decompress(byte[] encoded, int sampleCount, int bitsPerCode,
                       float p1, float p2, CancellationToken token);
}
```

- `Compress` و `Decompress` تعملان على **قناة واحدة**؛ التعامل مع
  ستيريو يتم في `Form1.RunCompression` بحلقة فوق القنوات.
- `GetBitsPerCode` يعيد عدد البتات لكل رمز بعد تطبيق الإعدادات.
- `GetParams` يحدّد المعاملَين اللذَين سيُكتبان في رأس `.amcx` (وتفسيرهما
  يختلف حسب الخوارزمية).

#### `Compression/CompressorFactory.cs`

```csharp
public static IAudioCompressor Create(CompressionAlgorithm alg);
```

نقطة واحدة لإنشاء المرمّز المطلوب. هذا يحرّر الواجهة من معرفة الأنواع
الملموسة — كل ما عليها هو معرفة قيمة الـ enum.

#### `Compression/NonlinearQuantizationCompressor.cs`

ينفّذ خوارزمية \u00B5-law:
1. **Compress**: لكل عينة، نعاير إلى `[-1, 1]`، نطبّق المعادلة
   `F(x) = sign(x) · ln(1 + \u00B5·|x|) / ln(1 + \u00B5)`، ثم نكمّم إلى
   `2^bits` مستوى ونكتب الرمز.
2. **Decompress**: نقرأ الرمز، نعيد بناء القيمة المعايَرة، ثم نعكس
   المعادلة بـ `(exp(magnitude · ln(1+\u00B5)) - 1) / \u00B5`.

المعامل `param1 = Mu`، و `param2 = 0`.

#### `Compression/DpcmCompressor.cs`

ينفّذ DPCM (Differential PCM):
1. يحتفظ بـ `prediction` ابتدائياً = 0.
2. لكل عينة: `diff = sample - prediction`، يكمّم `diff` إلى `bits` بت
   موقّعة، يكتب الرمز.
3. يبني `reconstructed = prediction + (code * step)` ويستخدمه كتنبؤ
   للعينة التالية. **هذا مهم**: يجب أن يستخدم المرمّز نفس القيمة التي
   سيراها فاك المرمّز، وإلا انحرفا تدريجياً.
4. عند فك الضغط: نقرأ كل رمز، نعكس الرمز إذا كان سالباً
   (`signed >= half ? signed - levels : signed`)، ونضيفه إلى `prediction`.

كلا الطرفين يطبّقان نفس قاعدة التشبّع `[-32768, 32767]`.

#### `Compression/DeltaModulationCompressor.cs`

ينفّذ DM البسيط: بت واحد لكل عينة. القيمة المعاد بناؤها تتحرّك بمقدار
`step` صعوداً أو نزولاً حسب علاقة العينة بها. المعامل `param1 = StepSize`،
و `param2 = 0`.

#### `Compression/AdaptiveDeltaModulationCompressor.cs`

ينفّذ ADM: مثل DM لكن `step` يتغيّر بحسب تتابع البتات:
- إذا كان البت الحالي = البت السابق ⇒ `step *= 1.5` (تسريع).
- وإلا ⇒ `step *= 0.75` (تباطؤ).
- `step` محدود بـ `[MinStepSize, MaxStepSize]`.

`param1 = InitialStep`، `param2 = MaxStep`. الـ `MinStep` يُشتقّ في فك
الضغط بقسمة `InitialStep` على 4 لتجنّب تخزين حقل ثالث.

---

### 3.4 طبقة الواجهة

#### `Form1.Designer.cs`

ملف مولَّد يدوياً (كتبناه كاملاً بدلاً من السماح للمصمم البصري بإدارته)
وظيفته إنشاء كل عناصر الواجهة وترتيبها هندسياً. كل عنصر له موقع وحجم
ثابتان (مع `Anchor` مناسب للسماح بالتمدد عند تغيير حجم النافذة).

الدوال المساعدة:

- `InitializeComponent()` — تنشئ النافذة وتضع كل العناصر فيها.
- `NewLabel(text, x, y, width)` — ينشئ Label منسّقاً بشكل موحّد.
- `AddPropertyRow(parent, text, out valueLbl, x, y, lblW, valW)` — يضيف
  صفاً من نوع "اسم الخاصية" + "قيمة" في لوحة الخصائص.
- `CreateChart(seriesName, title, xTitle, yTitle, color)` — ينشئ
  `System.Windows.Forms.DataVisualization.Charting.Chart` مهيّأً مسبقاً
  بنمط واحد متّسق (SplineArea + grid فاتح + خط حدود + ماركر دائري).

#### `Form1.cs`

أكبر ملف في المشروع وهو "غرفة التحكم" التي تربط كل شيء. ينقسم إلى أقسام
معلَّمة بتعليقات لتسهيل التنقّل:

##### الحقول الرئيسية

```csharp
private AudioFile _loadedAudio;                       // الـ WAV المحمَّل
private CompressedFileFormat.Container _compressedOutput; // آخر ناتج ضغط أو ‎.amcx مفتوح
private short[][] _decompressedSamples;               // آخر ناتج فك ضغط
private int _decompressedSampleRate;
private CompressionResult _lastResult;                // للتقرير
private OutputKind _outputKind;                       // None/Compressed/Decompressed
private CompressionSettings _defaultSettings;         // لإعادة الضبط
private CancellationTokenSource _cts;                 // للإلغاء
private bool _busy;                                   // لمنع التداخل
private AudioPlayer _player;                          // مشغل المعاينة
private string _decompressedPreviewPath;              // ملف WAV مؤقّت للمعاينة
```

ثم مجموعة `_btn...Color` تخزّن اللون الأصلي لكل زر ليُستعاد عند تفعيل
الزر، ويُستبدل بالرمادي عند تعطيله.

##### مجموعات الدوال (مرتّبة كما هي في الملف)

| المجموعة | الدوال الأساسية | الوظيفة |
|---------|------------------|--------|
| الإعداد | `CaptureButtonColors`, `WireEvents`, `PopulateAlgorithmCombo`, `ApplyDefaultSettings` | تُستدعى من المنشئ مرة واحدة عند إقلاع التطبيق. |
| تحميل الملفات | `BtnOpen_Click`, `BtnOpenCompressed_Click`, `AnyControl_DragEnter/DragDrop`, `LoadAudioFile`, `LoadCompressedFile`, `UpdateFilePropertyDisplay` | كل ما يتعلق بفتح ملف من القرص أو بالسحب. |
| المعاينة | `BtnPlay/Pause/Stop_Click`, `StopPlayback`, `PlaybackTimer_Tick`, `UpdatePlaybackLabel` | يتحكم في `_player` ويحدّث عدّاد الوقت كل 250ms. |
| الإعدادات | `CmbAlgorithm_SelectedIndexChanged`, `UpdateSettingsControlsEnabled`, `BtnResetSettings_Click`, `ApplySettingsToUi`, `ReadSettingsFromUi` | يفعّل / يعطّل الحقول حسب الخوارزمية، ويقرأ / يكتب الإعدادات. |
| الضغط | `BtnCompress_Click`, `RunCompression` | يطلق العملية في `Task.Run` ويبلّغ التقدم. |
| فك الضغط | `BtnDecompress_Click`, `RunDecompression` | نفس النمط لفكّ الضغط، ومع فك الضغط يحفظ WAV مؤقّتاً ويُحمَّل في المشغّل. |
| الحفظ والإلغاء | `BtnSave_Click`, `BtnCancel_Click` | يعرض `SaveFileDialog` ويستدعي `Write`؛ زر الإلغاء يُلغي `_cts`. |
| التقدم والتقارير | `OnProgress`, `ResetCharts`, `AddChartPointFinal`, `AddChartPoint`, `WriteReport`, `AppendReportLine`, `EstimateContainerSize` | يحدّث الرسوم والشريط ويبني نص التقرير. |
| حالة الأزرار | `StyleButton`, `SetBusy`, `UpdateButtonState` | يدير `Enabled` و `BackColor` لكل الأزرار في مكان واحد. |
| مساعدات عامة | `FormatBytes`, `Form1_FormClosing` | تنسيق الحجم، وتنظيف الموارد عند الإغلاق. |

##### الدوال الأكثر أهمية بالتفصيل

**`LoadAudioFile(path)`**:
1. يوقف المعاينة، يغلق `_player`، ويقرأ ملف WAV عبر `WavReader.Read`.
2. يضبط الحقول الداخلية ويُفرغ الإخراج والتقارير السابقة.
3. **يقيّد** `numSampleRate.Maximum = audio.SampleRate` — هذا يمنع المستخدم
   من إدخال معدل عينات أعلى من المصدر (الذي سيؤدي إلى رفع المعدل artificially).
4. يحمّل الملف في `_player` للمعاينة.
5. يستدعي `UpdateButtonState()` ليفعّل الأزرار المناسبة.

**`LoadCompressedFile(path)`**:
1. يقرأ الحاوية عبر `CompressedFileFormat.Read`.
2. يعرض البيانات الوصفية في لوحة الخصائص.
3. **يستدعي `ApplySettingsToUi(_defaultSettings)` أولاً** ليمحو القيم
   المتبقية من تشغيل سابق، **ثم** يحقن قيم الحاوية في الحقول المعنية. هذا
   يمنع وضعاً مربكاً مثل ظهور قيمة \u00B5 قديمة تحت ملف DM.
4. يلغي تقييد `numSampleRate.Maximum` (يعيده إلى 96000) لأن المستخدم لا
   ينوي ضغط شيء جديد الآن.

**`BtnCompress_Click()`** (الدالة الأهم):

```csharp
1. التحقق من وجود _loadedAudio.
2. settings = ReadSettingsFromUi();
3. settings.TargetSampleRate = Math.Min(target, source); // أمان مزدوج
4. SetBusy(true, "Compressing..."); ResetCharts(); ClearReport();
5. _cts = new CancellationTokenSource();
6. var container = await Task.Run(() => RunCompression(...));
7. عند النجاح:
     _compressedOutput = container;
     _lastResult = new CompressionResult { ... };
     WriteReport(_lastResult);
     يفعّل زر Save Output.
   عند الإلغاء أو الخطأ:
     يفرّغ الحالة ويعرض رسالة مناسبة.
8. finally: SetBusy(false);
```

**`RunCompression(audio, settings, sw, progress, token)`** يعمل على
خيط الخلفية:

```csharp
1. compressor = CompressorFactory.Create(settings.Algorithm);
2. لكل قناة في audio:
     resampled[c] = Resampler.Resample(...);
3. لكل قناة:
     channelData[c] = compressor.Compress(
         resampled[c], settings,
         progress(delta => يحدّث ProcessedSamples ويُبلِّغ ProgressUpdate),
         token);
4. يبني ويعيد Container جديدة.
```

**التقدم** يُبَلَّغ عبر `IProgress<long>` (عدد العينات الجديدة)، ثم يُحوَّل
في `Form1` إلى `ProgressUpdate` كامل ويُسلَّم إلى `OnProgress` على خيط
الواجهة (لأن `Progress<T>` يلتقط `SynchronizationContext` تلقائياً).

**`OnProgress(p)`** يقوم بأمرين:
1. يحدّث الـ `ProgressBar` والـ Status label دائماً.
2. يضيف نقطة جديدة في كل رسم بياني، **لكن** مع تخفيف معدل يمنع الإضافة
   أكثر من مرة كل 25ms لتجنّب إبطاء الواجهة على ملفات ضخمة.

**`StyleButton(btn, activeColor, foreColor, enabled)`** يضمن أن الزر يبدو
رمادياً عندما يكون معطّلاً (لأن أزرار FlatStyle لا تتعتم تلقائياً).
`UpdateButtonState()` يستدعي `StyleButton` لكل زر بناءً على الحالة
الحالية (هل هناك WAV محمَّل؟ ‎.amcx؟ هل المعالجة جارية؟).

#### `Program.cs`

نقطة دخول قياسية:

```csharp
Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.Run(new Form1());
```

---

## 4. تدفق البيانات (Sequence Diagrams)

### 4.1 سيناريو: ضغط ملف WAV

```
User                Form1                  Services / Compression
 │                    │                              │
 │ Drop file ─────────►│                              │
 │                    │ WavReader.Read(path) ────────►│
 │                    │◄──────── AudioFile ───────────│
 │                    │                              │
 │ Configure settings │                              │
 │ Click Compress ───►│                              │
 │                    │ Task.Run(RunCompression)     │
 │                    │  ├─► Resampler.Resample(ch1) │
 │                    │  ├─► CompressorFactory.Create│
 │                    │  ├─► compressor.Compress(ch1)│
 │                    │  │     progress reports ────►│ OnProgress(p)
 │                    │  │                           │   ├─► ProgressBar
 │                    │  │                           │   ├─► chartRatio
 │                    │  │                           │   └─► chartSpeed
 │                    │  └─► compressor.Compress(ch2)│
 │                    │◄──── Container               │
 │                    │ WriteReport(result) ─────────►│ txtReport
 │ Click Save Output ►│                              │
 │                    │ CompressedFileFormat.Write   │
 │ ◄─── .amcx saved ──│                              │
```

### 4.2 سيناريو: فك ضغط ملف ‎.amcx

```
User                Form1                   Services / Compression
 │                    │                              │
 │ Open .amcx ────────►│                              │
 │                    │ CompressedFileFormat.Read ──►│
 │                    │◄────── Container ────────────│
 │                    │ ApplySettingsToUi(defaults)  │
 │                    │ Inject container values      │
 │ Click Decompress ─►│                              │
 │                    │ Task.Run(RunDecompression)   │
 │                    │  └─► لكل قناة:               │
 │                    │       compressor.Decompress  │
 │                    │       (channel data → short[]│
 │                    │◄──── short[][] samples       │
 │                    │ WavWriter.Write(temp.wav) ──►│
 │                    │ AudioPlayer.Load(temp.wav) ─►│
 │ Click Play ───────►│ AudioPlayer.Play()           │
 │ Click Save Output ►│                              │
 │                    │ WavWriter.Write(user path) ─►│
 │ ◄─── WAV saved ────│                              │
```

---

## 5. كيف تضيف خوارزمية جديدة؟

لو أراد أحدكم إضافة خوارزمية خامسة (مثلاً Predictive Differential Coding):

1. **أضف قيمة جديدة** في `CompressionAlgorithm` enum مع رقم فريد:
   ```csharp
   PredictiveDifferential = 4
   ```
   وأضف حالتها في `ToFriendlyString()`.

2. **أنشئ فئة جديدة** في `Compression/` تنفّذ `IAudioCompressor`:
   - حدِّد `Algorithm` لتُعيد القيمة الجديدة.
   - نفّذ `GetBitsPerCode`, `GetParams`, `Compress`, `Decompress`.
   - استخدم `BitWriter` لكتابة الرموز و `BitReader` لقراءتها.

3. **سجّل الخوارزمية** في `CompressorFactory.Create`:
   ```csharp
   case CompressionAlgorithm.PredictiveDifferential:
       return new PredictiveDifferentialCompressor();
   ```

4. **حدّث ‎.csproj** لإضافة الملف الجديد:
   ```xml
   <Compile Include="Compression\PredictiveDifferentialCompressor.cs" />
   ```

5. (اختياري) في `Form1.cs > UpdateSettingsControlsEnabled` فعّل / عطّل
   الحقول التي تستخدمها الخوارزمية الجديدة.

**هذا كل شيء** — لا حاجة لتعديل الواجهة أو الحاوية أو منطق التقدم؛ كلها
ستعمل تلقائياً مع المرمّز الجديد عبر `IAudioCompressor`.

---

## 6. كيف تختبر التغييرات يدوياً؟

1. **بناء المشروع** في Visual Studio (F6 أو Build → Build Solution).
2. **تشغيل** بـ F5 (يبدأ النموذج Form1).
3. **اختبار سيناريو الضغط**:
   - اسحب ملف `.wav` صغير (ثانية أو ثانيتان) إلى النافذة.
   - تحقق من ظهور كل خصائص الملف.
   - اضغط Play وتأكد من سماع الصوت.
   - اختر خوارزمية، اضبط الإعدادات، اضغط Compress.
   - تأكد من أن الرسمين البيانيين يمتلئان أثناء الضغط.
   - راجع التقرير، احفظ الملف.
4. **اختبار سيناريو فك الضغط**:
   - افتح الملف ‎.amcx الذي حفظته.
   - تأكد من أن البيانات الوصفية صحيحة.
   - اضغط Decompress، ثم Play لسماع النسخة المُعاد بناؤها.
   - احفظ كـ WAV واستمع له خارج التطبيق.

5. **اختبار الإلغاء**: ابدأ ضغط ملف كبير، اضغط Cancel، تأكد من توقّف
   العملية وأن الواجهة لم تتعطّل.

6. **اختبار إعادة الضبط**: غيّر الإعدادات، اضغط Reset to Original، تأكد
   من عودة معدل أخذ العينات إلى قيمة المصدر.

---

## 7. مفاتيح وفخاخ مهمة

- **العينات تُخزَّن دائماً كـ `short` 16 بت موقّع داخلياً**، حتى لو كان المصدر
  8 بت. الخوارزميات تفترض هذا.
- **كل خوارزمية تعمل على قناة واحدة**. التعامل مع ستيريو يتم بحلقة في
  `RunCompression`. لا تجعل خوارزمية تعرف عن القنوات.
- **العمليات الطويلة تعمل على خيط خلفية** (`Task.Run`). لا تستدعِ
  عناصر الواجهة من داخلها مباشرة — استخدم `IProgress<T>`.
- **زر FlatStyle لا يتعتم تلقائياً عند تعطيله**؛ هذا هو سبب وجود
  `StyleButton`.
- **معدل العينات الهدف لا يمكن أن يتجاوز المصدر** — هذا قرار تصميمي،
  ليس خطأ. النموذج يقيّد `Maximum` تلقائياً.
- **`Progress<T>` يتزامن تلقائياً مع خيط الواجهة** إذا أُنشئ عليه، لذا
  دوال مثل `OnProgress` آمنة على UI thread.
- **CancellationToken يجب أن يُفحَص داخل الحلقات** وإلا لن يستجيب الإلغاء.
- **ملف ‎.amcx يصف نفسه بنفسه**: يحفظ نوع الخوارزمية والمعاملات والمعدل
  وعدد القنوات. فلا حاجة لأي معلومات خارجية لفك ضغطه.

---

## 8. أين تجد كل شيء؟ (خريطة سريعة)

| تريد أن تفهم / تعدّل... | اذهب إلى |
|--------------------------|----------|
| كيف تُقرَأ ملفات WAV | `Services/WavReader.cs` |
| كيف تُكتَب ملفات WAV | `Services/WavWriter.cs` |
| كيف يُشغَّل الصوت | `Services/AudioPlayer.cs` |
| صيغة الملف المضغوط | `Services/CompressedFileFormat.cs` |
| منطق خوارزمية NLQ / DPCM / DM / ADM | `Compression/*Compressor.cs` |
| تحزيم البتات | `Compression/BitWriter.cs` و `BitReader.cs` |
| تخطيط الواجهة (مواقع وأحجام) | `Form1.Designer.cs` |
| كيف يتفاعل المستخدم مع الواجهة | `Form1.cs` (مقسَّم لأقسام معلَّمة) |
| الإعدادات التي يمكن للمستخدم ضبطها | `Models/CompressionSettings.cs` |
| ما يُعرَض في التقرير | `Form1.WriteReport` |
| كيف يُحدَّث الرسم البياني | `Form1.OnProgress` و `Form1.AddChartPoint` |

---

أي سؤال؟ راجع الكود؛ التعليقات الموجودة فيه تشرح **لماذا** اتُّخذ كل قرار
تصميمي، وليس فقط ما تفعله الشيفرة.
