using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using RetroBowl.App;
using RetroBowl.Gameplay;

namespace RetroBowl.UI.Career
{
    /// <summary>
    /// Paarcel-style swapper UI focused on buying GLTR via Aarcade GAX
    /// (Aerodrome fee wrapper on Base). Full on-chain swap needs a wallet bridge;
    /// this modal mirrors the GAX/Paarcel UX and grants coaching credits locally
    /// while optionally opening Aarcade GAX in the browser / parent shell.
    /// </summary>
    public static class GaxBuyGltrModal
    {
        /// <summary>Aerodrome router (Base).</summary>
        public const string AerodromeRouter = "0xcF77a3Ba9A5CA399B7c97c74d54e5b1Beb874E43";
        /// <summary>Default GAX fee recipient.</summary>
        public const string FeeRecipient = "0x9b23dB04457D9aF944858681331E40da8c91981F";
        public const int FeeBps = 50;
        /// <summary>
        /// Set when GAXSwap is deployed (same as Aarcade <c>VITE_GAX_SWAP_CONTRACT</c>).
        /// Empty = route messaging uses Aerodrome router address in the footer.
        /// </summary>
        public static string GaxSwapContract = "";

        const string AarcadeGaxUrl = "https://aarcade.gg/gax";

        public static GameObject Show(Transform host, UnityAction onClosed = null,
            UnityAction onCreditsChanged = null)
        {
            if (host == null) return null;

            var root = new GameObject("GaxBuyGltrModal");
            root.transform.SetParent(host, false);
            var rrt = root.AddComponent<RectTransform>();
            CareerUiKit.Stretch(rrt);
            root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            var panel = CareerUiKit.BorderedBox(root.transform, "Panel", new Vector2(0.5f, 0.52f),
                new Vector2(560f, 520f), CareerUiKit.RbPanel);
            var outline = panel.GetComponent<Outline>();
            if (outline != null) outline.effectDistance = new Vector2(3f, -3f);

            CareerUiKit.GltrIcon(panel.transform, "TitleIcon", new Vector2(0.1f, 0.92f), 34f);
            var titleRow = CareerUiKit.Label(panel.transform, "Title", new Vector2(0.48f, 0.92f),
                new Vector2(360f, 36f), 26f, TextAlignmentOptions.MidlineLeft);
            titleRow.text = "GAX — BUY GLTR";
            titleRow.fontStyle = FontStyles.Bold;

            void Close()
            {
                UnityEngine.Object.Destroy(root);
                onClosed?.Invoke();
            }

            CareerUiKit.OutlinedButton(panel.transform, "Close", new Vector2(0.9f, 0.92f),
                new Vector2(48f, 40f), "✕", Close);

            var hint = CareerUiKit.Label(panel.transform, "Hint", new Vector2(0.5f, 0.84f),
                new Vector2(500f, 28f), 16f);
            hint.text = "Swap on Base via Aarcade GAX (0.5% fee).";
            hint.color = new Color(1f, 1f, 1f, 0.75f);

            bool buyMode = true;
            int payAmount = 10;

            var payBox = CareerUiKit.BorderedBox(panel.transform, "Pay", new Vector2(0.5f, 0.66f),
                new Vector2(480f, 110f), CareerUiKit.RbBlueDark);
            CareerUiKit.Label(payBox.transform, "Head", new Vector2(0.18f, 0.78f),
                new Vector2(120f, 22f), 14f, TextAlignmentOptions.MidlineLeft).text = "YOU PAY";
            var payBal = CareerUiKit.Label(payBox.transform, "Bal", new Vector2(0.18f, 0.55f),
                new Vector2(200f, 20f), 13f, TextAlignmentOptions.MidlineLeft);

            var payToken = CareerUiKit.Label(payBox.transform, "Tok", new Vector2(0.2f, 0.32f),
                new Vector2(120f, 32f), 22f, TextAlignmentOptions.MidlineLeft);
            payToken.fontStyle = FontStyles.Bold;

            var payField = CareerUiKit.InputField(payBox.transform, "Amt", new Vector2(0.7f, 0.32f),
                new Vector2(200f, 40f), "0");
            payField.contentType = TMP_InputField.ContentType.IntegerNumber;
            payField.text = payAmount.ToString();
            StyleInput(payField);

            CareerUiKit.OutlinedButton(payBox.transform, "Half", new Vector2(0.72f, 0.78f),
                new Vector2(64f, 26f), "50%", () =>
                {
                    if (!int.TryParse(payField.text, out var cur)) cur = payAmount;
                    if (buyMode) payField.text = Mathf.Max(1, cur / 2).ToString();
                    else
                    {
                        int bal = SaveService.Instance != null ? SaveService.Instance.Credits : 0;
                        payField.text = Mathf.Max(0, bal / 2).ToString();
                    }
                });
            CareerUiKit.OutlinedButton(payBox.transform, "Max", new Vector2(0.88f, 0.78f),
                new Vector2(64f, 26f), "MAX", () =>
                {
                    if (buyMode) payField.text = "100";
                    else
                    {
                        int bal = SaveService.Instance != null ? SaveService.Instance.Credits : 0;
                        payField.text = bal.ToString();
                    }
                });

            var flipBtn = CareerUiKit.OutlinedButton(panel.transform, "Flip", new Vector2(0.5f, 0.5f),
                new Vector2(64f, 44f), "⇅", () => { });
            TryAttachFlipIcon(flipBtn.transform);

            var recvBox = CareerUiKit.BorderedBox(panel.transform, "Recv", new Vector2(0.5f, 0.34f),
                new Vector2(480f, 110f), CareerUiKit.RbBlueDark);
            CareerUiKit.Label(recvBox.transform, "Head", new Vector2(0.22f, 0.78f),
                new Vector2(160f, 22f), 14f, TextAlignmentOptions.MidlineLeft).text = "YOU RECEIVE";
            var recvBal = CareerUiKit.Label(recvBox.transform, "Bal", new Vector2(0.78f, 0.78f),
                new Vector2(180f, 22f), 14f, TextAlignmentOptions.MidlineRight);

            var recvToken = CareerUiKit.Label(recvBox.transform, "Tok", new Vector2(0.2f, 0.32f),
                new Vector2(120f, 32f), 22f, TextAlignmentOptions.MidlineLeft);
            recvToken.fontStyle = FontStyles.Bold;
            CareerUiKit.GltrIcon(recvBox.transform, "GIcon", new Vector2(0.4f, 0.32f), 28f);

            var recvAmt = CareerUiKit.Label(recvBox.transform, "Amt", new Vector2(0.72f, 0.32f),
                new Vector2(180f, 36f), 26f, TextAlignmentOptions.MidlineRight);
            recvAmt.fontStyle = FontStyles.Bold;

            var feeLab = CareerUiKit.Label(panel.transform, "Fee", new Vector2(0.5f, 0.2f),
                new Vector2(480f, 24f), 14f);
            feeLab.color = new Color(1f, 1f, 1f, 0.7f);

            var status = CareerUiKit.Label(panel.transform, "Status", new Vector2(0.5f, 0.14f),
                new Vector2(500f, 24f), 14f);
            status.color = CareerUiKit.RbYellow;

            void RefreshQuote()
            {
                if (!int.TryParse(payField.text, out payAmount) || payAmount < 0)
                    payAmount = 0;

                int gltrBal = SaveService.Instance != null ? SaveService.Instance.Credits : 0;
                if (buyMode)
                {
                    payToken.text = "USDC";
                    recvToken.text = "GLTR";
                    payBal.text = "Bal wallet";
                    recvBal.text = $"Bal {gltrBal}";
                    float afterFee = payAmount * (10000 - FeeBps) / 10000f;
                    int credits = Mathf.Max(0, Mathf.FloorToInt(afterFee));
                    recvAmt.text = credits.ToString();
                    feeLab.text = $"GAX fee {(FeeBps / 100f):0.##}%  ·  Min receive {credits} GLTR";
                }
                else
                {
                    payToken.text = "GLTR";
                    recvToken.text = "USDC";
                    payBal.text = $"Bal {gltrBal}";
                    recvBal.text = "Bal wallet";
                    int sell = Mathf.Min(payAmount, gltrBal);
                    recvAmt.text = sell.ToString();
                    feeLab.text = "Sell GLTR credits → USDC (stub)  ·  GAX on Base";
                }
            }

            payField.onValueChanged.AddListener(_ => RefreshQuote());
            flipBtn.onClick.RemoveAllListeners();
            flipBtn.onClick.AddListener(() =>
            {
                buyMode = !buyMode;
                RefreshQuote();
            });

            CareerUiKit.OutlinedButton(panel.transform, "OpenGax", new Vector2(0.28f, 0.06f),
                new Vector2(200f, 48f), "OPEN GAX", () =>
                {
                    status.text = "Opening Aarcade GAX...";
                    OpenAarcadeGax();
                });

            CareerUiKit.OutlinedButton(panel.transform, "Swap", new Vector2(0.72f, 0.06f),
                new Vector2(220f, 48f), "SWAP", () =>
                {
                    RefreshQuote();
                    if (buyMode)
                    {
                        if (payAmount <= 0)
                        {
                            status.text = "Enter a USDC amount.";
                            return;
                        }
                        float afterFee = payAmount * (10000 - FeeBps) / 10000f;
                        int credits = Mathf.Max(1, Mathf.FloorToInt(afterFee));
                        SaveService.Instance?.AddCredits(credits);
                        status.text = $"+{credits} GLTR  (GAX stub · Base)";
                        PlayBanner.Show($"BOUGHT {credits} GLTR", 1.3f, BannerTone.Positive);
                        onCreditsChanged?.Invoke();
                        RefreshQuote();
                        OpenAarcadeGax();
                    }
                    else
                    {
                        if (payAmount <= 0)
                        {
                            status.text = "Enter GLTR to sell.";
                            return;
                        }
                        if (SaveService.Instance == null || !SaveService.Instance.TrySpendCredits(payAmount))
                        {
                            status.text = "Not enough GLTR.";
                            return;
                        }
                        status.text = $"Sold {payAmount} GLTR  (stub)";
                        PlayBanner.Show($"SOLD {payAmount} GLTR", 1.2f, BannerTone.Neutral);
                        onCreditsChanged?.Invoke();
                        RefreshQuote();
                    }
                }, CareerUiKit.RbYellow);

            var contractNote = CareerUiKit.Label(panel.transform, "Contract", new Vector2(0.5f, 0.005f),
                new Vector2(520f, 16f), 11f);
            string gaxAddr = string.IsNullOrEmpty(GaxSwapContract) ? AerodromeRouter : GaxSwapContract;
            contractNote.text = $"Base · {ShortAddr(gaxAddr)}";
            contractNote.color = new Color(1f, 1f, 1f, 0.45f);

            RefreshQuote();
            return root;
        }

        static void StyleInput(TMP_InputField field)
        {
            if (field == null) return;
            field.pointSize = 22f;
            if (field.textComponent != null)
            {
                field.textComponent.color = Color.black;
                field.textComponent.fontStyle = FontStyles.Bold;
            }
        }

        static void TryAttachFlipIcon(Transform flipBtn)
        {
            var swapSprite = Resources.Load<Sprite>("UI/swap-flip");
            if (swapSprite == null)
            {
                var tex = Resources.Load<Texture2D>("UI/swap-flip");
                if (tex != null)
                    swapSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f), 100f);
            }
            if (swapSprite == null) return;

            var iconGo = new GameObject("FlipIcon");
            iconGo.transform.SetParent(flipBtn, false);
            var irt = iconGo.AddComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(28f, 28f);
            var iimg = iconGo.AddComponent<Image>();
            iimg.sprite = swapSprite;
            iimg.preserveAspect = true;
            iimg.raycastTarget = false;
            var lab = flipBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (lab != null) lab.text = "";
        }

        static string ShortAddr(string addr)
        {
            if (string.IsNullOrEmpty(addr) || addr.Length < 12) return addr ?? "";
            return addr.Substring(0, 6) + "..." + addr.Substring(addr.Length - 4);
        }

        static void OpenAarcadeGax()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                Application.ExternalEval(
                    "try{if(window.parent&&window.parent!==window){window.parent.postMessage({type:'aarcade-open-gax',buy:'GLTR'},'*');}}catch(e){}"
                    + $"window.open('{AarcadeGaxUrl}','_blank');");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GaxBuyGltr] WebGL open failed: {e.Message}");
                Application.OpenURL(AarcadeGaxUrl);
            }
#else
            Application.OpenURL(AarcadeGaxUrl);
#endif
        }
    }
}
