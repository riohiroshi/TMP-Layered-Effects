# TMP Layered Effects

[English](README.md) | **繁體中文**

透過距離場合成與快取，為 Unity **TextMeshProUGUI** 加上多層描邊、漸層與柔和陰影。

![原生 TMP 與三種多層文字效果對照](Docs/demo.png)

本儲存庫是一個完整的 Unity 展示專案。渲染元件處於從遊戲專案獨立出來的早期階段，尚不能全面取代 TMP 的原生渲染功能。

## 開啟展示場景

1. 透過 Unity Hub 安裝 **Unity 6000.3.13f1**。
2. 在 Unity Hub 選擇 **Add project from disk**（從磁碟加入專案），指定本儲存庫根目錄，也就是包含 `Assets`、`Packages` 與 `ProjectSettings` 的資料夾。
3. 開啟 `Assets/Demo/LayeredEffects.unity`。
4. 切換到 **Game** 頁籤，使用 **1280 × 900** 或相近的長寬比。左側是原生 TMP，右側是三種多層效果；繁體中文、日文與英文皆使用同一款 CC0 字型。支援編輯模式預覽；進入 Play Mode 時使用同一個展示場景。
5. 選取 `Demo Canvas` 下的 `Effect …` 物件，在 Inspector 調整 TMP 文字，或 `Layered Text Compositor` 的顏色、半徑與陰影設定。

所有可見文字，包括介面標籤及中、日、英文範例，都使用作者以 **CC0 1.0** 釋出的 **Jigmo（字雲）**。本專案未附帶其他字型檔。

## 隨附字型

- **Jigmo.ttf**：明朝／宋體風格的襯線字型，單一字重，三列語言範例皆使用此字型。
- **官方來源與 CC0 聲明：**[Jigmo 作者網站](https://kamichikoichi.github.io/jigmo/)
- **CC0 條款：**[CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/)
- **來源紀錄與校驗碼：**[FONT-SOURCES.json](Docs/FONT-SOURCES.json)。

### 商用與再散布

依作者的 CC0 公有領域貢獻宣告，隨附字型可用於商業用途、嵌入遊戲、複製、修改及再散布，不必另行申請個別許可或支付字型授權費。CC0 不要求署名或保留版權聲明。本專案自願保留原始 README 與 CC0 條款作為來源證據，不是對字型使用者附加限制。

我們已核對作者官方 CC0 聲明，確認下載的壓縮檔與作者官方 GitHub 儲存庫中的 Git blob 相符，並記錄隨附檔案的 SHA-256 雜湊值。作者網站顯示的校驗碼不完整，且與目前壓縮檔不符，因此我們**不宣稱通過該網頁校驗碼驗證**。完整證據請見[來源紀錄](Docs/FONT-SOURCES.json)。

上述核對確認的是字型所宣告的使用條件，以及交付檔案的來源；不構成權利歸屬或不侵權保證，也不保證第三方永遠不會提出主張。

隨附檔案涵蓋基本多文種平面（BMP）的字元與部分補充字元，未包含獨立的 Jigmo2／Jigmo3 擴充字型。生成的 TMP SDF 字型資產已預先載入展示文字與可列印 ASCII 字元，並啟用動態填入圖集，以便加入來源字型支援的其他字元。三列展示使用同一字型搭配不同效果，並非依語言自動選擇字型。由於此字型不包含 U+FF01（全形驚嘆號），範例使用 ASCII `!`。

標準 TMP Shader 仍是獨立的 Unity 相依資源，適用其自身條款。CC0 聲明僅適用於**字型**，不涵蓋本專案程式碼、Unity 或所有第三方軟體。

## 在其他專案使用

將 `Assets/TMPLayeredEffects/Runtime` 與 `Assets/TMPLayeredEffects/Shaders` 連同各自的 `.meta` 檔，複製到已安裝 Unity UI／TextMeshPro 的專案。`Editor` 資料夾僅包含展示生成與驗證工具，可選擇不匯入。

1. 在 Canvas 下建立 **TextMeshPro - Text (UI)** 物件，並指定 SDF 字型。
2. 加入 `TMPLayeredEffects.LayeredTextCompositor` 元件。
3. 在 **Build dependencies** 指定三個 Shader：`LayeredTextMask`、`LayeredTextDistance`、`LayeredTextComposite`。請將這些引用保存在場景或 Prefab 中，確保 Player 建置包含這些 Hidden Shader。`Shader.Find` 只是找不到明確引用時的備用方式。
4. 調整內外圈半徑、顏色、陰影偏移與柔和程度。

`innerRadius` 與 `outerRadius` 都是從字面向外延伸的距離，而非各圈獨立的厚度。外圈半徑應大於或等於內圈半徑。合成元件會管理一個暫存的 `RawImage` 子物件，並在啟用時隱藏原始 TMP 渲染器；停用元件即可看見原始文字。

若透過程式修改快取未自動追蹤的排版屬性，請主動要求重新整理：

```csharp
using TMPLayeredEffects;

// 修改 TMP 排版或其他未自動追蹤的屬性後：
compositor.MarkDirty();
```

## 運作方式

1. 將 TMP 字形網格與字型圖集中的覆蓋資訊繪製成字面紋理。
2. 透過多輪 Jump Flood 處理，近似計算每個像素到最近已覆蓋像素的距離。
3. 將陰影、外圈、內圈與字面合成至一張快取的 RenderTexture。
4. 持續顯示該紋理，直到受追蹤的文字屬性改變，或呼叫 `MarkDirty()`。

渲染倍率控制的是快取解析度，並不提供不受螢幕 DPI 影響的呈現保證。此做法適合偶爾更新的文字，例如回合開始標語與靜態標題。重建時會執行多輪涵蓋整張紋理的 GPU 處理；最終只顯示一張 UI 圖片，**不代表整個操作只需要一次 draw call**。

## 目前限制

- 展示環境以 Unity 6、uGUI、SDF 字型與 Built-in Render Pipeline 為主。其他版本、渲染管線、平台與圖形 API 仍需驗證。
- 固定採用兩圈描邊，尚無任意效果堆疊或預設樣式資產系統。
- 並非為世界空間的 `TextMeshPro` 網格渲染器、點陣字型或行內 Sprite 相容性而設計。
- 未完整重現 TMP 材質設定，例如字面膨脹與字重。
- 快取更新判斷會追蹤文字、字型／材質物件身分、矩形尺寸、字級與顏色；尚未完整追蹤間距、對齊、邊界留白、材質內容或逐字網格動畫。
- 大幅縮放 Transform 時，可能看出快取紋理解析度的限制。尚未整合逐字動畫。
- 尚未全面測試遮罩、備援字型組合或第三方動畫工具。
- 尚無效能實測或全面相容性保證。大型快取與頻繁重建可能產生較高成本。
- 沒有圖形裝置時，合成元件會保留原始 TMP 的啟用狀態；合成效果本身需要 GPU。

## 專案結構

- `Assets/TMPLayeredEffects/Runtime/` — 獨立的文字合成元件。
- `Assets/TMPLayeredEffects/Shaders/` — 字面、距離場與合成 Shader。
- `Assets/TMPLayeredEffects/Editor/` — 展示生成與 GPU 驗證工具。
- `Assets/Demo/LayeredEffects.unity` — 已建立的效果對照場景，已加入 Build Settings。
- `Assets/Demo/Fonts/jigmo/` — 原始 CC0 字型、來源文件與生成的 TMP SDF 資產。
- `Assets/TextMesh Pro/` — 標準 TMP Shader／設定資源；原附帶字型已移除。
- `Docs/PROVENANCE.md` — 程式碼提取歷史與變更紀錄。
- `Docs/VALIDATION.md` — 實際驗證項目與尚未驗證的範圍。

`Tools > TMP Layered Effects > Create Demo Scene` 會重新生成展示場景，並將截圖與驗證報告寫入已被 Git 忽略的 `Artifacts` 目錄。此操作會取代展示場景，且需要圖形裝置；請勿搭配 `-nographics` 執行。

## 授權

專案程式碼依現有的 [MIT 授權](LICENSE) 提供。隨附的 Jigmo 字型採 CC0。Unity／TMP 資源仍適用各自的上游條款，詳見[第三方資源說明](Docs/THIRD-PARTY-NOTICES.md)。
