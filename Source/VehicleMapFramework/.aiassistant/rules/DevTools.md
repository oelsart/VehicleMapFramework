# DevTools テスト作成スキル(RimWorld Mod 向け)

DevTools は、RimWorld のゲーム内で動くテストフレームワーク。実際のワールドとマップを生成するか、セーブをロードして、その中でテストする。
この文書は、他の Mod 用にテストクラスを書くための要点をまとめたもの。ソースで確認した公開 API だけを載せている。未確認の事項は「未確認」と書いてある。

- 名前空間: `DevTools.Testing`
- テストはメインスレッドで 1 つずつ同期実行される。同時に 2 つのランナーは動かせない。
- テストは `UNITY_ASSERTIONS` が定義されたビルドで実行する前提とする。

## 1. 基本形

```csharp
using System.Collections;
using DevTools.Testing;

namespace MyMod.Tests;

[TestFixture(TestType.Playing)]
[TestCategory("Utils")]
internal sealed class Test_Xxx
{
  public Test_Xxx() { }            // public コンストラクタはちょうど 1 つ

  [OneTimeSetUp]
  private void SetUpFixture() { }  // 戻り値は void 必須

  [SetUp]
  private void SetUpEachTest() { }

  [TearDown]
  private void TearDownEachTest() { }

  [OneTimeTearDown]
  private void TearDownFixture() { }

  [Test]
  private void SomeBehavior()
  {
    Expect.IsTrue(true);
  }

  [Test]
  private IEnumerator WaitOneFrame()   // IEnumerator を返すとコルーチンとして実行される
  {
    yield return null;
    Expect.IsTrue(true);
  }
}
```

- クラスに `[TestFixture(TestType)]` を付ける。メソッドは `private` でもよい(non-public も探索される)。
- フィクスチャの登録条件: `abstract` でないこと、必須 Mod が揃っていること、**`public` なコンストラクタがちょうど 1 つ**あること。
  満たさないとエラーログが出て登録されない。コンストラクタを省略した場合の扱いは未確認。書くなら `public` にする。
- 同じキー(`名前空間.型名` と引数)のフィクスチャは、2 つ目以降が追加されない。
- `[SetUp]` / `[TearDown]` / `[OneTimeSetUp]` / `[OneTimeTearDown]` は戻り値が `void` でなければならない。違反するとそのメソッドは登録されない。コルーチンにはできない。
- 同じ種類の前後処理メソッドは複数定義できる。前後処理の中で `Expect` が失敗したり、例外が出たりすると、テストの失敗として扱われる。
- 実行順(`ExecutionPriority`)
    - 値が **大きいほど先** に実行される。未指定は 0(`Priority.Normal` と同じ)。同じ値どうしの順序は保証されない。
    - フィクスチャ間は、まず `TestType` の宣言順(`MainMenu` → `Playing` → `PostGameExit`)で並ぶ。
    - フィルタを付けて実行した場合(`--where` など)は、クラスの `ExecutionPriority` が **無視される**。フィクスチャの実行順に依存するテストは書かない。
    - 順序に依存するなら、同じクラスの `[Test]` メソッドに `ExecutionPriority` を付ける。

## 2. TestType

| 値 | 動作 |
|---|---|
| `MainMenu` | メインメニュー上で実行する。 |
| `Playing` | ワールドとマップを生成して、プレイ中に実行する。 |
| `PostGameExit` | 生成済みのゲームがなければ生成し、メインメニューに戻って実行する。 |

## 3. 属性

属性は `DevTools.Testing` にある。C# の属性構文では `Attribute` の接尾辞を省略して書く。

| 属性 | 対象 | 内容 |
|---|---|---|
| `[TestFixture(TestType)]` | class | フィクスチャ宣言。 |
| `[Test]` | method | テストメソッド。 |
| `[TestCase(params object[])]` | method | 引数付きテスト。複数付けられる。`ExpectedResult` で戻り値を比較できる。 |
| `[SetUp]` / `[TearDown]` | method | 各テストの前後。 |
| `[OneTimeSetUp]` / `[OneTimeTearDown]` | method | フィクスチャの前後に 1 回。 |
| `[ExecutionPriority(Priority)]` / `[ExecutionPriority(int)]` | class / method | 実行優先度。 |
| `[Retry(ushort)]` | method のみ | 失敗時のリトライ回数。 |
| `[LoadSave(string)]` | class のみ | 生成せず、そのセーブをロードして実行する。 |
| `[Disabled]` | class / method | 実行対象から外す。 |
| `[TestCategory(params string[])]` | class / method | カテゴリ(任意の文字列)。 |
| `[TestDescription(string)]` | class / method | UI のツールチップ。 |
| `[WarningsAllowed(params string[])]` | class / method | 失敗扱いにしない警告の正規表現。 |
| `[ErrorsAllowed(params string[])]` | class / method | 失敗扱いにしないエラーの正規表現。 |
| `[LoadIfModsActive(params string[])]` | class / method | 指定した Mod がすべて有効なときだけ登録する。 |
| `[LoadIfAnyModsActive(params string[])]` | class / method | 指定した Mod のどれかが有効なときだけ登録する。 |
| `[Parameters(params object[])]` | parameter | 値の列挙。値 1 つが 1 ケース。 |
| `[ParametersSource(string)]` / `[ParametersSource(Type, string)]` | parameter | 名前付きのメンバーから値を供給する。 |
| `[DefsSource(params string[] packageIds)]` | parameter | 引数の型に一致する Def をすべて供給する。 |
| `[DefsSource(Type, string filterName, params string[] packageIds)]` | parameter | 同上。`filterName` の static bool メソッドでフィルタする。 |

`Priority`: `Last = int.MinValue`, `BelowNormal = -100`, `Normal = 0`, `AboveNormal = 100`, `First = int.MaxValue`

- `DefParametersAttribute` は使うとコンパイルエラーになる。`DefsSource` を使う。
- `HideInUIAttribute` は `internal` なので、他アセンブリからは使えない。
- 必須 Mod は、クラスにもメソッドにも付けられる。欠けていれば登録されない。

### 3.1 引数付きテストの規則

- **`[TestCase]` が 1 つでもあれば最優先**。その場合、引数ソース属性(`Parameters` など)は見られない。
- 引数ソース属性を使う場合、引数に **ソース属性が必須**で、**引数は 1 つだけ**。2 つ以上や属性なしでは、エラーログが出て引数なしとして扱われる。
- `[ParametersSource]`
    - `Type` を省略すると、メソッドを宣言した型から探す。
    - フィールドは **`static` の配列型**(`List<T>` は不可)。メソッドは **`static` で引数なし**(`IEnumerable` を返す)。
- `[DefsSource]`: packageId は **小文字で書く**(指定側は小文字化されない)。実行時に追加された Def やモック Def(`modContentPack` が null)は除外される。
- フィクスチャのコンストラクタ引数にも同じ仕組みが使える。ケースごとにフィクスチャが 1 つ作られる。

```csharp
[TestCase(1, 2, ExpectedResult = 3)]
[TestCase(2, 5, ExpectedResult = 7)]
private int Add(int a, int b) => a + b;

[Test]
private void CheckDef([DefsSource("author.modid")] ThingDef def)
{
  Expect.IsNotNull(def);
}
```

- `ExpectedResult` の比較は、通常の(コルーチンでない)テストでだけ行われる。`float` / `double` / `decimal` は近似比較。
- 上の `TestCase` の例は実装から導いたもの。既存テストでの使用例は未確認。

## 4. アサーション

### 4.1 `Expect`(失敗を記録するが、例外は投げない)

失敗してもテストメソッドは最後まで実行される。最初の失敗の情報が残る。ランナーの外で呼ぶと、何もせずエラーログを出す。

- 真偽・null: `IsTrue` / `IsFalse` / `IsNull` / `IsNotNull` / `That(Func<bool>)`
- コレクション: `IsEmpty` / `IsNotEmpty` / `All` / `Any` / `None`
- 比較: `AreEqual<T>` / `AreNotEqual` / `AreApproximatelyEqual` / `AreNotApproximatelyEqual` / `ReferencesAreEqual<T>` / `ReferencesAreNotEqual<T>` / `GreaterThan` / `GreaterThanOrEqualTo` / `LessThan` / `LessThanOrEqualTo`
- 例外: `Throws<T>(Action)`(例外を返す) / `DoesNotThrow(Action)`
- 最後の引数 `message` は省略可能。

### 4.2 `UnityEngine.Assertions.Assert`(前提条件に使う)

- 失敗すると `AssertionException` を投げる。テストランナーがそれを捕まえて失敗にするので、**そのテストメソッドはそこで打ち切られる**。
- 使い分けの目安: 前提の確認に `Assert`、結果の検証に `Expect`。
- `Assert` は `[Conditional("UNITY_ASSERTIONS")]` 付きなので、引数に副作用のある式を書かない。

### 4.3 `Test` 静的クラス

- `Test.Fail(string)` / `Test.Fail(Exception)` / `Test.Skip(string)` / `Test.Cancel(string)`: 結果を記録する。**例外は投げない**ので、必要なら自分で `return` する。
- `Test.Suspend(float, string)`: UI ダイアログを出すため、ヘッドレス実行には向かない。

## 5. ログによる失敗判定

- テスト中の Warning / Error は、`Expect` が何も失敗していなくても **失敗扱い**になる(既定で警告もエラーも失敗)。`Log.Message` は失敗にならない。
- 例外や `Assert` のログもエラー扱い。
- 許可したいログは、属性に正規表現で指定する。

```csharp
[ErrorsAllowed(@"Something expected")]
[WarningsAllowed(@"Could not find a place")]
[Test]
private void SomeTest() { }
```

- クラスに付けた `WarningsAllowed` / `ErrorsAllowed` は、そのクラスの全テストに効く。
- 設定ファイル `UnitTestConfig` の `warningsAllowed` / `errorsAllowed` でも、全体に対して許可できる。
- マップ生成で出る `GenStep_Scatterer` の警告は、`GenStepWarningDisabler`(第 8 章)で抑えられる。

## 6. フィクスチャごとの生成設定

フィクスチャが次のインターフェースを実装すると、ゲームの生成設定を上書きできる。

| インターフェース | プロパティ |
|---|---|
| `IWorldGeneration` | `WorldGenerationSettings` |
| `IMapGeneration` | `MapGenerationSettings` |
| `IScenario` | `Scenario` |
| `IStoryteller` | `Storyteller` |

- どれか 1 つでも非 null を返すと、そのフィクスチャのために環境が再生成される。次のフィクスチャでは元に戻る。
- 指定がなければ、`UnitTestConfig` の `world` / `map` の設定が使われる。
- `MapGenerationSettings` のメンバー: `biome`, `size`, `mapGeneratorDef`, `extraGenStepDefs`, `Faction`, `postGenerationActions`, `Default`
- `WorldGenerationSettings` のメンバー: `percent`, `rainfall`, `temperature`, `population`, `landmarkDensity`, `startingSeason` など
- `[LoadSave]` を使うと、生成せずにセーブをロードする。生成設定との併用は警告になる。

## 7. 設定ファイル `UnitTestConfig`(`TestConfig`)

| フィールド | 既定値 | 内容 |
|---|---|---|
| `randSeed` | なし | 乱数のシード。未指定なら `Rand.Int`。 |
| `world` / `map` | 既定の設定 | 生成設定。 |
| `failOnWarnings` / `failOnErrors` | `true` | ログで失敗にするか。 |
| `warningsAllowed` / `errorsAllowed` | 空 | 許可するログの正規表現。 |
| `stopOnFailure` | `false` | 失敗したら実行を止める。 |
| `retryOnFailure` | `false` | `true` なら全テストを 1 回リトライ。 |
| `preTestActions` / `postTestActions` | 空 | フィクスチャごとの前後処理。 |

- 設定ファイルは、テストを持つ Mod ごとに読み込まれる。設定が読めなければ、その Mod のテストは登録されない。
- リトライ回数は、`[Retry]` と `retryOnFailure` の大きいほう。

## 8. 補助クラス

### 8.1 DevTools 共通(`DevTools.Testing`、他 Mod から使ってよい)

| クラス | 用途 |
|---|---|
| `ScopeWorldObject(WorldObject)` | `using` を抜けるときに、WorldObject を `Destroy()` する。 |
| `ScopeEntity(Thing)` | `using` を抜けるときに、Thing を `Destroy()` する。 |
| `GenStepWarningDisabler` | `GenStep_Scatterer.warnOnFail` を一時的に無効にする。`using` で使う。 |

```csharp
using ScopeWorldObject worldObjectScope = new(worldObject);
using GenStepWarningDisabler genStepDisabler = new();
```

- `GenStepWarningDisabler` は二重に `Disable()` すると、元の値が `false` で上書きされる(コードを読んだだけで、実行は未確認)。二重に使わない。
- 生成したオブジェクトは、`using` のスコープで確実に破棄する書き方が参照実装の流儀。

### 8.2 Vehicles 固有(他の Mod では使えない。参考)

- `Vehicles.Tests.VehicleGroup`: 車両と搭乗者のまとまり。`IDisposable`。
    - `VehicleGroup.CreateBasicVehicleGroup(MockSettings)` で作る。`MockSettings` で車両の定義や人数を指定する。
    - `Spawn()` は、搭乗者を先に全員乗せてから車両をスポーンする。
    - 他に `BoardOne` / `BoardAll` / `DisembarkOne` / `DisembarkAll` / `SpawnPawns` / `DeSpawn` / `DeSpawnPawns` がある。
    - `Dispose()` で搭乗者と車両を `Destroy()` する。内部で `Assert` を使っている。
- `Vehicles.Tests.TestUtils`: 車両テスト用のユーティリティ。`ForceSpawn(vehicle)` を使っているのを確認した。それ以外の中身は未確認。
- `Vehicles.SaveTester`: `Write()` / `WriteAndLoad()` / `Clear()` で、テスト用セーブの書き出しとロードを行う。
- `SmashTools.Tests.TestUtils` という別のクラスもあるが、中身は未確認。
- `TestCategoryNames` は、各テストプロジェクトが自前で定義する(DevTools 共通ではない)。`[TestCategory]` は任意の文字列を受け取る。

## 9. 実行方法

### 9.1 ゲーム内

テストエクスプローラ(`Dialog_TestExplorer`)から実行する。UI は `Disabled` なテストを表示しない。

### 9.2 コマンドライン

ゲームの起動引数に指定する。

| 引数 | 内容 |
|---|---|
| `--pid <packageId>` | **必須**。対象 Mod の packageId(大文字小文字は無視)。一致しない Mod のテストは起動しない。 |
| `--test` / `-t` | 単体テスト(既定。省略可)。 |
| `--smoke-test` / `-s` | スモークテスト。 |
| `--test-plan <名前>` / `-p <名前>` | テストプランで実行する。 |
| `--test-list <ファイル>` | ファイルに書いた `FullName` を 1 行 1 件で指定して実行する。 |
| `--where <式>` / `-w <式>` | 条件式で絞り込む(9.3)。 |
| `--exit` / `-e` | 終了時にゲームを終了する。失敗があれば終了コード 1、なければ 0。 |
| `-NoWindow` | ウィンドウを出さずに実行する。`--exit` も有効になる。 |
| `--port <n>` `--session <id>` | 結果を TCP で外部に送る。 |

例: `--pid author.modid -t -e`

- `--test-list`、`--port`、`--session` は、引数が最後の位置にあると範囲外アクセスになりうる。必ず値を続ける。

### 9.3 `--where` の式

- 形式: `キー 演算子 値`。条件は `&&` / `||`(`and` / `or` も可)で連結する。
- 例: `type == Playing && category == Utils`
- 優先順位はなく、**左から順に結合される**。括弧は使えない。
- 演算子: `==` / `!=`(完全一致)、`=~`(含む)、`!~`(含まない)。`equals` / `notequals` / `matches` / `nomatches` も可。
- 値は引用符なしで書く。使える文字は英数字、`_`、`.`、`-`。**空白や引用符を含む値は指定できない**(引用符は値の一部として残るため、一致しない)。

| キー | 比較対象 |
|---|---|
| `category` | `[TestCategory]` のどれか。 |
| `method` | メソッド名、`型名.メソッド名`、`完全型名.メソッド名`。 |
| `class` | 型名、完全型名、アセンブリ修飾名。 |
| `namespace` | フィクスチャの名前空間。 |
| `type` | `TestType` の名前(`Playing` など)。大文字小文字を区別する。 |
| `status` | 実行状態(`Failed` など)。大文字小文字は区別しない。 |
| その他 | メタデータのキーとして扱う。値がなければ偽。 |

- 絞り込んだ実行では、クラスの `ExecutionPriority` は無視される(第 1 章)。

## 10. 書くときの注意

- テストは実際のゲームの中で動く。Unity の API はメインスレッドから呼ぶ。
- ヘッドレス実行では、GUI の描画に依存しない。`Test.Suspend` のような UI を出す API は使わない。
- 1 つのテストで複数の失敗を見たいなら `Expect`、前提が崩れたら先に進めないなら `Assert`。
- 生成したオブジェクトは、`using` スコープなどで確実に後始末する。
- ログの Warning / Error が出るだけで失敗になる。意図的に出す場合は、`[ErrorsAllowed]` / `[WarningsAllowed]` を付ける。
- テストプロジェクトは、`UNITY_ASSERTIONS` が定義されたビルド構成で実行する。

## 11. 未確認

- `--where` を複数のフィクスチャで使ったとき、`OrderBy(Type.Assembly)` が `IComparable` 未実装で例外にならないか(コード上は危うく見えるが、実行は未確認)。`--where` で `ArgumentException` が出たら、ここを疑う。
- テストプロジェクトの `.csproj` の標準的な書き方(DevTools の参照方法、出力先、`DevTools.Analyzer` の導入)。
- `Test.GenerateReport` と `Config.log.report` の出力形式。
- `SmashTools.Tests.TestUtils` と `Vehicles.Tests.TestUtils`(`ForceSpawn` 以外)の中身。
```