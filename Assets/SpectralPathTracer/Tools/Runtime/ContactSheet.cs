using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace SpectralPathTracer
{
    // renders the scene locked at each wavelength from 400 to 700 nm in Play Mode and tiles the results into one labeled image
    [DisallowMultipleComponent]
    public class ContactSheet : MonoBehaviour
    {
        #region Constants

        private const float FirstWavelength = 400f; // nm, the first tile
        private const float Step = 20f; // nm between tiles
        private const int Columns = 4;
        private const int Rows = 4; // 16 tiles, so the last one is 700 nm

        private const int GlyphScale = 2; // sheet pixels per font pixel
        private const int LabelMargin = 4; // sheet pixels between a label and its tile's corner
        private const string OutputFolder = "Recordings/ContactSheets";

        #endregion

        #region Private Fields

        // 3 by 5 glyphs, one string per row from the top, # for a lit pixel
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            { '0', new[] { "###", "#.#", "#.#", "#.#", "###" } },
            { '1', new[] { ".#.", "##.", ".#.", ".#.", "###" } },
            { '2', new[] { "###", "..#", "###", "#..", "###" } },
            { '3', new[] { "###", "..#", ".##", "..#", "###" } },
            { '4', new[] { "#.#", "#.#", "###", "..#", "..#" } },
            { '5', new[] { "###", "#..", "###", "..#", "###" } },
            { '6', new[] { "###", "#..", "###", "#.#", "###" } },
            { '7', new[] { "###", "..#", "..#", ".#.", ".#." } },
            { '8', new[] { "###", "#.#", "###", "#.#", "###" } },
            { '9', new[] { "###", "#.#", "###", "..#", "###" } },
            { 'n', new[] { "...", "##.", "#.#", "#.#", "#.#" } },
            { 'm', new[] { "...", "###", "###", "#.#", "#.#" } }
        };

        private int _tile;
        private int _stepReport;
        private bool _capturing;
        private bool _wasRunInBackground;

        private Color32[] _sheet;
        private Vector2Int _sheetSize;
        private Vector2Int _tileSize;

        #endregion

        #region Inspector Fields

        [field: Tooltip("Samples per pixel in every tile.")]
        [field: Min(1)]
        [field: SerializeField] public int SamplesPerTile { get; private set; } = 64;

        [field: Tooltip("GPU time to aim for per Unity frame while the sheet renders, in milliseconds.")]
        [field: Min(1f)]
        [field: SerializeField] public float FrameBudgetMs { get; private set; } = 250f;

        #endregion

        private void OnEnable()
        {
            _wasRunInBackground = Application.runInBackground;
            Application.runInBackground = true;

            PathTracerOverrides.Spectral = true;
            PathTracerOverrides.TargetSamples = SamplesPerTile;
            PathTracerOverrides.FrameBudgetMs = FrameBudgetMs;

            _tile = 0;
            _capturing = false;
            _sheet = null;


            StepTo(_tile);
        }

        private void OnDisable()
        {
            Application.runInBackground = _wasRunInBackground;
            PathTracerOverrides.Clear();
        }

        private void Update()
        {
            // a report from before the step still describes the last tile
            if (_capturing || AccumulationProgress.Reports == _stepReport || AccumulationProgress.Samples < SamplesPerTile)
                return;

            _capturing = true;
            DisplayCapture.Request(OnCaptured);
        }

        /**
         * <summary>
         * Places a finished tile in the sheet and moves on to the next wavelength.
         * </summary>
         * <param name="pixels">The tile's sRGB pixels, bottom row first.</param>
         * <param name="size">The capture's size in pixels.</param>
         */
        private void OnCaptured(Color32[] pixels, Vector2Int size)
        {
            // a capture can still land after the sheet was stopped
            if (!enabled)
                return;

            // the sheet is the size of one capture, so the grid exactly fills a frame
            if (_sheet == null)
            {
                _sheetSize = size;
                _tileSize = new Vector2Int(size.x / Columns, size.y / Rows);
                _sheet = new Color32[size.x * size.y];
            }

            PlaceTile(pixels, size, _tile);
            DrawLabel($"{Wavelength(_tile):F0}nm", _tile);
            Debug.Log($"Contact sheet: tile {_tile + 1} of {Columns * Rows} at {Wavelength(_tile):F0} nm");

            _capturing = false;
            _tile++;
            if (_tile >= Columns * Rows)
            {
                Write();
                enabled = false;
                return;
            }


            StepTo(_tile);
        }

        #region Helpers

        private static float Wavelength(int tile) => FirstWavelength + tile * Step;

        /**
         * <summary>
         * Locks the next tile's wavelength and starts its accumulation from the first sample.
         * </summary>
         * <param name="tile">The tile to render, 0 for the top left.</param>
         */
        private void StepTo(int tile)
        {
            PathTracerOverrides.LockedWavelength = Wavelength(tile);
            PathTracerOverrides.RequestRestart();
            _stepReport = AccumulationProgress.Reports;
        }

        /**
         * <summary>
         * Averages each block of capture pixels into one tile pixel, so a full frame shrinks into its cell of the grid.
         * </summary>
         * <param name="pixels">The capture, bottom row first.</param>
         * <param name="size">The capture's size in pixels.</param>
         * <param name="tile">The tile's index, 0 for the top left.</param>
         */
        private void PlaceTile(Color32[] pixels, Vector2Int size, int tile)
        {
            int blockX = size.x / _tileSize.x; // capture pixels per tile pixel across, 4 at 1920 by 1080
            int blockY = size.y / _tileSize.y;
            int count = blockX * blockY;

            // the grid fills from the top left, while pixel rows count from the bottom
            int originX = (tile % Columns) * _tileSize.x;
            int originY = (Rows - 1 - tile / Columns) * _tileSize.y;

            for (int y = 0; y < _tileSize.y; y++)
            {
                for (int x = 0; x < _tileSize.x; x++)
                {
                    // a box filter: the plain average of the block, which keeps the tile's brightness honest
                    int r = 0, g = 0, b = 0;
                    for (int by = 0; by < blockY; by++)
                    {
                        for (int bx = 0; bx < blockX; bx++)
                        {
                            Color32 pixel = pixels[(y * blockY + by) * size.x + x * blockX + bx];
                            r += pixel.r;
                            g += pixel.g;
                            b += pixel.b;
                        }
                    }

                    _sheet[(originY + y) * _sheetSize.x + originX + x] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
                }
            }
        }

        /**
         * <summary>
         * Stamps a label into a tile's top left corner, white with a black outline.
         * </summary>
         * <param name="text">The label, drawn from the glyph table.</param>
         * <param name="tile">The tile's index, 0 for the top left.</param>
         */
        private void DrawLabel(string text, int tile)
        {
            int left = (tile % Columns) * _tileSize.x + LabelMargin;
            int top = (Rows - tile / Columns) * _tileSize.y - 1 - LabelMargin; // the tile's top row, counted from the bottom

            // a black copy grown by one pixel first, so the white text reads on bright and dark tiles alike
            DrawText(text, left, top, 1, new Color32(0, 0, 0, 255));
            DrawText(text, left, top, 0, new Color32(255, 255, 255, 255));
        }

        private void DrawText(string text, int left, int top, int grow, Color32 color)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!Glyphs.TryGetValue(text[i], out string[] glyph))
                    continue;

                int glyphLeft = left + i * 4 * GlyphScale; // 3 font pixels wide plus 1 of spacing
                for (int row = 0; row < glyph.Length; row++)
                {
                    for (int column = 0; column < glyph[row].Length; column++)
                    {
                        if (glyph[row][column] == '#')
                            FillBlock(glyphLeft + column * GlyphScale - grow, top - (row + 1) * GlyphScale + 1 - grow, GlyphScale + 2 * grow, color);
                    }
                }
            }
        }

        private void FillBlock(int left, int bottom, int size, Color32 color)
        {
            for (int y = bottom; y < bottom + size; y++)
            {
                for (int x = left; x < left + size; x++)
                {
                    if (x >= 0 && y >= 0 && x < _sheetSize.x && y < _sheetSize.y)
                        _sheet[y * _sheetSize.x + x] = color;
                }
            }
        }

        private void Write()
        {
            Directory.CreateDirectory(OutputFolder);
            string path = Path.Combine(OutputFolder, $"{gameObject.scene.name}.png");

            byte[] png = ImageConversion.EncodeArrayToPNG(_sheet, GraphicsFormat.R8G8B8A8_SRGB, (uint)_sheetSize.x, (uint)_sheetSize.y);
            File.WriteAllBytes(path, png);
            Debug.Log($"Contact sheet saved to {path}");
        }

        #endregion
    }
}
