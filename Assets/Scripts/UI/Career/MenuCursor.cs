using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RetroBowl.Gameplay;

namespace RetroBowl.UI.Career
{
    /// <summary>
    /// Controller / keyboard highlight cursor for career menus.
    /// D-pad / arrows move · Start / A / Space / Enter confirms (mouse still uses Button clicks).
    /// </summary>
    public class MenuCursor
    {
        public struct Cell
        {
            public Button Button;
            public GameObject Visual;
            public System.Action OnConfirm;
            public bool Enabled;

            public static Cell FromButton(Button btn, System.Action onConfirm = null)
            {
                return new Cell
                {
                    Button = btn,
                    Visual = btn != null ? btn.gameObject : null,
                    OnConfirm = onConfirm,
                    Enabled = btn != null && btn.interactable
                };
            }
        }

        Cell[,] grid;
        int rows;
        int cols;
        int row;
        int col;
        bool active;

        public bool IsActive => active;
        public int Row => row;
        public int Col => col;
        public int Rows => rows;
        public int Cols => cols;

        public void Bind(Cell[,] cells, int startRow = 0, int startCol = 0)
        {
            ClearAllHighlights();
            grid = cells;
            rows = cells != null ? cells.GetLength(0) : 0;
            cols = cells != null && rows > 0 ? cells.GetLength(1) : 0;
            row = Mathf.Clamp(startRow, 0, Mathf.Max(0, rows - 1));
            col = Mathf.Clamp(startCol, 0, Mathf.Max(0, cols - 1));
            EnsureOnEnabledCell();
            if (active)
                RefreshHighlight();
        }

        /// <summary>Flat button list laid out left→right, top→bottom in <paramref name="columns"/>.</summary>
        public void BindButtons(IList<Button> buttons, int columns = 1, int startIndex = 0)
        {
            if (buttons == null || buttons.Count == 0)
            {
                ClearAllHighlights();
                grid = null;
                rows = cols = 0;
                return;
            }

            var list = new List<Button>(buttons.Count);
            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                if (b == null || !b.interactable) continue;
                if (!b.gameObject.activeInHierarchy) continue;
                list.Add(b);
            }

            if (list.Count == 0)
            {
                ClearAllHighlights();
                grid = null;
                rows = cols = 0;
                return;
            }

            int colsN = Mathf.Max(1, columns);
            int rowsN = (list.Count + colsN - 1) / colsN;
            var cells = new Cell[rowsN, colsN];
            for (int i = 0; i < list.Count; i++)
            {
                int r = i / colsN;
                int c = i % colsN;
                cells[r, c] = Cell.FromButton(list[i]);
            }

            int start = Mathf.Clamp(startIndex, 0, list.Count - 1);
            Bind(cells, start / colsN, start % colsN);
        }

        /// <summary>All interactable buttons under <paramref name="root"/> (depth-first).</summary>
        public void BindUnder(Transform root, int columns = 1, int startIndex = 0)
        {
            if (root == null)
            {
                BindButtons(null);
                return;
            }

            BindButtons(root.GetComponentsInChildren<Button>(true), columns, startIndex);
        }

        public void SetActive(bool on)
        {
            if (active == on)
            {
                if (active) RefreshHighlight();
                return;
            }

            active = on;
            if (!active)
                ClearAllHighlights();
            else
            {
                EnsureOnEnabledCell();
                RefreshHighlight();
            }
        }

        public void Tick()
        {
            if (!active || grid == null || rows == 0 || cols == 0) return;

            if (TecmoInput.MoveUpDown())
                Move(-1, 0);
            else if (TecmoInput.MoveDownDown())
                Move(1, 0);
            else if (TecmoInput.MoveLeftDown())
                Move(0, -1);
            else if (TecmoInput.MoveRightDown())
                Move(0, 1);
            else if (TecmoInput.MenuConfirmDown())
                ActivateFocused();
        }

        public void Move(int dRow, int dCol)
        {
            if (rows == 0 || cols == 0) return;
            if (dRow == 0 && dCol == 0) return;

            int guard = rows * cols;
            int r = row;
            int c = col;
            for (int i = 0; i < guard; i++)
            {
                r = (r + dRow + rows) % rows;
                c = (c + dCol + cols) % cols;
                if (!IsEnabled(r, c)) continue;
                row = r;
                col = c;
                RefreshHighlight();
                return;
            }

            // No enabled neighbor in that direction — stay put.
        }

        public void ActivateFocused()
        {
            if (!IsEnabled(row, col)) return;
            var cell = grid[row, col];
            if (cell.OnConfirm != null)
            {
                cell.OnConfirm.Invoke();
                return;
            }

            if (cell.Button != null && cell.Button.interactable)
                cell.Button.onClick.Invoke();
        }

        void EnsureOnEnabledCell()
        {
            if (IsEnabled(row, col)) return;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (!IsEnabled(r, c)) continue;
                row = r;
                col = c;
                return;
            }
        }

        bool IsEnabled(int r, int c)
        {
            if (grid == null || r < 0 || c < 0 || r >= rows || c >= cols)
                return false;
            var cell = grid[r, c];
            if (!cell.Enabled || cell.Visual == null) return false;
            if (cell.Button != null && !cell.Button.interactable) return false;
            return cell.Visual.activeInHierarchy;
        }

        void RefreshHighlight()
        {
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                ApplyHighlight(grid[r, c].Visual, r == row && c == col && IsEnabled(r, c));
        }

        void ClearAllHighlights()
        {
            if (grid == null) return;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                ApplyHighlight(grid[r, c].Visual, false);
        }

        public static void ApplyHighlight(GameObject go, bool focused)
        {
            if (go == null) return;
            var outline = go.GetComponent<Outline>();
            if (outline == null)
                outline = go.AddComponent<Outline>();
            outline.effectColor = focused ? CareerUiKit.RbYellow : Color.white;
            // Save-select style yellow focus — keep it heavy so it reads on dark navy cards.
            outline.effectDistance = focused ? new Vector2(16f, -16f) : new Vector2(2f, -2f);
            outline.enabled = true;

            var rt = go.transform as RectTransform;
            if (rt != null)
                rt.localScale = focused ? Vector3.one * 1.06f : Vector3.one;

            var padSwapper = go.GetComponent<PadHighlightSwapper>();
            if (padSwapper != null)
                padSwapper.SetCursorFocused(focused);
        }
    }
}
