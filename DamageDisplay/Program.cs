using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        DebugAPI Draw;
        SEUtils _seu;
        MyIni _ini;
        float localVertexMultiplier;
        SortedDictionary<Vector3, GridBlock> _gridBlocks;
        Dictionary<TextPanelRenderingContext, List<LCDWithRotation>> _textPanelRenderingContexts = new Dictionary<TextPanelRenderingContext, List<LCDWithRotation>>();
        MySprite PixelSprite = new MySprite()
        {
            Data = "SquareSimple",
            Color = Color.White,
            Size = new Vector2(1, 1)
        };
        int loopCount = 0;
        int maxLoopCount = 100;
        string INI_SECTION_HEADER = "Damage Display";
        string currentID = "main";

        public Program()
        {
            Draw = new DebugAPI(this);
            _seu = new SEUtils(this);
            _ini = new MyIni();

            if (_ini.TryParse(_seu.CurrentProgrammableBlock.CustomData) && !string.IsNullOrEmpty(_seu.CurrentProgrammableBlock.CustomData))
            {
                if (_ini.ContainsSection(INI_SECTION_HEADER))
                {
                    currentID = _ini.Get(INI_SECTION_HEADER, "Display_ID").ToString();
                }
            }
            else
            {
                _ini.AddSection(INI_SECTION_HEADER);
                _ini.SetSectionComment(INI_SECTION_HEADER, "Configuration for damage display controller");
                _ini.Set(INI_SECTION_HEADER, "Display_ID", "main");
                _ini.EndComment = "The PB with a certain ID displays only on LCD's with that ID.\nPlease configure";
                _seu.CurrentProgrammableBlock.CustomData = _ini.ToString();
                throw new InvalidOperationException("Please configure the PB in the custom data");
            }
            _ini.Clear();
            // Setup
            List<IMyTextPanel> allLCDsOnGrid = new List<IMyTextPanel>();
            // Get all lcd's on grid
            GridTerminalSystem.GetBlocksOfType(allLCDsOnGrid, _seu.IsInGrid);
            // select all the lcd's which have correct customdata
            List<LCDWithRotation> lcds = new List<LCDWithRotation>();
            foreach (var item in allLCDsOnGrid.Where(x => !string.IsNullOrEmpty(x.CustomData)))
            {
                if (_ini.TryParse(item.CustomData))
                {
                    if (_ini.ContainsSection(INI_SECTION_HEADER))
                    {
                        string vectorString = _ini.Get(INI_SECTION_HEADER, "View_rotation").ToString();
                        string ID = _ini.Get(INI_SECTION_HEADER, "Display_ID").ToString();
                        vectorString = vectorString.Substring(1, vectorString.Length - 2);
                        string[] numbers = vectorString.Split(' ').Select(x => x.Split(':')[1]).ToArray();
                        if (ID == currentID)
                        {
                            lcds.Add(new LCDWithRotation() { TextPanel = item, Rotation = new Vector3(MathHelper.ToRadians(float.Parse(numbers[0])), MathHelper.ToRadians(float.Parse(numbers[1])), MathHelper.ToRadians(float.Parse(numbers[2]))) });
                        }
                    }
                }
                else if (item.CustomData.ToLower().Contains("damage_display"))
                {
                    _ini.AddSection(INI_SECTION_HEADER);
                    _ini.SetSectionComment(INI_SECTION_HEADER, "Configuration for damage display");
                    _ini.Set(INI_SECTION_HEADER, "Display_ID", "main");
                    _ini.Set(INI_SECTION_HEADER, "Display_type", "3D");
                    _ini.SetComment(INI_SECTION_HEADER, "Display_type", "Options: [3D|2D]");
                    _ini.Set(INI_SECTION_HEADER, "View_rotation", new Vector3().ToString());
                    item.CustomData = _ini.ToString();
                }
                _ini.Clear();
            }
            // remove reference to temp list
            var uniqueLcdTypes = lcds.GroupBy(x => x.TextPanel.BlockDefinition.SubtypeId + x.Rotation.ToString());
            _textPanelRenderingContexts = uniqueLcdTypes.Select(x => new { CTX = new TextPanelRenderingContext(x.First().TextPanel, Vector3D.Backward), LCDs = x.ToList() }).ToDictionary(x => x.CTX, y => y.LCDs);
            allLCDsOnGrid = null;

            foreach (var item in _textPanelRenderingContexts.SelectMany(x => x.Value))
            {
                item.TextPanel.ContentType = ContentType.SCRIPT;
                item.TextPanel.ScriptBackgroundColor = Color.Black;
            }
            // Start everything
            _seu.StartCoroutine(StartSystem());
        }

        public void Main(string argument, UpdateType updateSource)
        {
            loopCount = 0;
            if (!_seu.RuntimeUpdate(argument, updateSource)) return;
        }

        IEnumerator StartSystem()
        {
            int id = _seu.StartCoroutine(GatherShipInfo());
            //yield return new WaitForNextTick();
            yield return new WaitForConditionMet(() => !_seu.CheckCoroutineRunning(id), 5000, 1000, () => { Echo("Script start timeouted"); return false; });

            // start main coroutine
            _seu.StartCoroutine(Main());
        }

        IEnumerator Main()
        {
            while (true)
            {
                Echo("Rendering...");
                // Rendering current info
                {
                    var blocksToRender = new HashSet<MyTuple<Vector3, GridBlock>>();
                    foreach (var item in _gridBlocks)
                    {
                        if (!item.Value.IsAir)
                        {

                        }

                        loopCount++;
                        if (loopCount >= maxLoopCount)
                        {
                            yield return new WaitForNextTick();
                        }
                    }
                    List<Vector3> verticiesToRender = new List<Vector3>();
                    int i = 0;
                    foreach (var block in blocksToRender)
                    {
                        verticiesToRender.AddRange(GridBlock.Vertices.Select(vertexLocal => vertexLocal + block.Key * 2.5f));

                        loopCount++;
                        if (loopCount >= maxLoopCount)
                        {
                            yield return new WaitForNextTick();
                        }
                    }
                    Echo("Gathering vertices finished");
                    foreach (var context in _textPanelRenderingContexts)
                    {
                        Echo("Rendering vertices...");
                        Vector3 rotation = context.Value.First().Rotation;
                        List<Vector3> renderedVerticies = new List<Vector3>();
                        List<Vector2> pixels = new List<Vector2>();

                        foreach (var vertex in verticiesToRender)
                        {
                            //try { throw new InvalidOperationException("break my point"); } catch (Exception) { }
                            Vector3D vertexToRotate = vertex * localVertexMultiplier;
                            vertexToRotate = Vector3D.Rotate(vertexToRotate, MatrixD.CreateRotationX(-rotation.X) * MatrixD.CreateRotationY(-rotation.Y) * MatrixD.CreateRotationZ(-rotation.Z));
                            var lpointToProject = vertexToRotate + Vector3D.Left * 2.4d + Vector3D.Down * 1.2f + Vector3D.Forward * 15;
                            if (!renderedVerticies.Contains(lpointToProject))
                            {
                                renderedVerticies.Add(lpointToProject);
                                var proj = context.Key.ProjectLocalPoint(lpointToProject);
                                if (proj != null)
                                {
                                    pixels.Add((Vector2)proj);
                                }
                            }

                            loopCount++;
                            if (loopCount >= maxLoopCount)
                            {
                                yield return new WaitForNextTick();
                            }
                        }
                        Echo("Rendering vertices finished: " + pixels.Count());
                        foreach (var lcd in context.Value)
                        {
                            Echo("LCD: " + lcd.TextPanel.CustomName);
                            var df = lcd.TextPanel.DrawFrame();
                            foreach (var pixel in pixels)
                            {
                                PixelSprite.Position = pixel;
                                df.Add(PixelSprite);

                                loopCount++;
                                if (loopCount >= maxLoopCount)
                                {
                                    yield return new WaitForNextTick();
                                }
                            }
                            df.Dispose();
                            Echo("Rendered verticies applied");
                        }
                    }
                }

                yield return new WaitForMilliseconds(2000);
            }
        }

        IEnumerator GatherShipInfo()
        {
            try { throw new InvalidOperationException("break my point"); } catch (Exception) { }
            Echo("Started gathering all blocks");
            Draw.DrawPoint(_seu.CurrentCubeGrid.GridIntegerToWorld(_seu.CurrentCubeGrid.Min), Color.Red, 0.2f, 10);
            Draw.DrawPoint(_seu.CurrentCubeGrid.GridIntegerToWorld(_seu.CurrentCubeGrid.Max), Color.Blue, 0.2f, 10);
            Vector3 gridSize = _seu.CurrentCubeGrid.Max - _seu.CurrentCubeGrid.Min;
            // maxFarOut * x = 2.4f -> 2.4f / maxFarOut = x
            float maxFarOut = gridSize.Max() / 2;
            localVertexMultiplier = 2.4f / maxFarOut;
            _gridBlocks = new SortedDictionary<Vector3, GridBlock>();
            for (int x = 0; x < gridSize.X; x++)
            {
                for (int y = 0; y < gridSize.Y; y++)
                {
                    for (int z = 0; z < gridSize.Z; z++)
                    {
                        var pos = _seu.CurrentCubeGrid.Min + new Vector3I(x, y, z);
                        var slim = _seu.CurrentCubeGrid.GetCubeBlock(pos);
                        _gridBlocks.Add(pos, new GridBlock() { Position = pos, SlimBlock = slim, TerminalBlock = slim as IMyTerminalBlock, Exists = _seu.CurrentCubeGrid.CubeExists(pos) }); ;

                        loopCount++;
                        if (loopCount >= maxLoopCount)
                        {
                            yield return new WaitForNextTick();
                        }
                    }
                }
            }
            Echo("Gathered all blocks");
        }

        bool IsOutside(GridBlock block)
        {
            foreach (var item in GridBlock.NeighbourBlocksGrid)
            {
                if (_gridBlocks.ContainsKey(item + block.Position) && _gridBlocks[block.Position + item].IsAir)
                    return true;
            }
            return false;
        }

        bool Raycast(Vector3 origin, Vector3 dir, float length, Matrix rot, out int instructions, float stepSize = 0.3f)
        {
            float currentLength = 0;
            int ins = 0;
            while (currentLength <= length)
            {
                ins++;
                currentLength += stepSize;
                var checkPos = origin + dir * currentLength;
                if (_gridBlocks.Where(x => Vector3.Distance(x.Key, checkPos) <= GridBlock.BlockSizeHalf * 1.3f).Any(x => x.Value.Mesh(rot).PointIntersects(checkPos)))
                {
                    instructions = ins;
                    return true;
                }
            }
            dir.Normalize();
            instructions = ins;
            return false;
        }

        IEnumerable<Vector3> DirectNeighbours(GridBlock gridBlock, Func<GridBlock, bool> predicate)
        {
            foreach (var item in GridBlock.NeighbourBlocksGrid)
            {
                if (_gridBlocks.ContainsKey(item + gridBlock.Position) && predicate(gridBlock))
                    yield return item + gridBlock.Position;
            }
        }

        public void Save()
        {

        }

        public class DebugAPI
        {
            public readonly bool ModDetected;

            public void RemoveDraw() => _removeDraw?.Invoke(_pb);
            Action<IMyProgrammableBlock> _removeDraw;

            public void RemoveAll() => _removeAll?.Invoke(_pb);
            Action<IMyProgrammableBlock> _removeAll;

            public void Remove(int id) => _remove?.Invoke(_pb, id);
            Action<IMyProgrammableBlock, int> _remove;

            public int DrawPoint(Vector3D origin, Color color, float radius = 0.2f, float seconds = DefaultSeconds, bool? onTop = null) => _point?.Invoke(_pb, origin, color, radius, seconds, onTop ?? _defaultOnTop) ?? -1;
            Func<IMyProgrammableBlock, Vector3D, Color, float, float, bool, int> _point;

            public int DrawLine(Vector3D start, Vector3D end, Color color, float thickness = DefaultThickness, float seconds = DefaultSeconds, bool? onTop = null) => _line?.Invoke(_pb, start, end, color, thickness, seconds, onTop ?? _defaultOnTop) ?? -1;
            Func<IMyProgrammableBlock, Vector3D, Vector3D, Color, float, float, bool, int> _line;

            public int DrawAABB(BoundingBoxD bb, Color color, Style style = Style.Wireframe, float thickness = DefaultThickness, float seconds = DefaultSeconds, bool? onTop = null) => _aabb?.Invoke(_pb, bb, color, (int)style, thickness, seconds, onTop ?? _defaultOnTop) ?? -1;
            Func<IMyProgrammableBlock, BoundingBoxD, Color, int, float, float, bool, int> _aabb;

            public int DrawOBB(MyOrientedBoundingBoxD obb, Color color, Style style = Style.Wireframe, float thickness = DefaultThickness, float seconds = DefaultSeconds, bool? onTop = null) => _obb?.Invoke(_pb, obb, color, (int)style, thickness, seconds, onTop ?? _defaultOnTop) ?? -1;
            Func<IMyProgrammableBlock, MyOrientedBoundingBoxD, Color, int, float, float, bool, int> _obb;

            public int DrawSphere(BoundingSphereD sphere, Color color, Style style = Style.Wireframe, float thickness = DefaultThickness, int lineEveryDegrees = 15, float seconds = DefaultSeconds, bool? onTop = null) => _sphere?.Invoke(_pb, sphere, color, (int)style, thickness, lineEveryDegrees, seconds, onTop ?? _defaultOnTop) ?? -1;
            Func<IMyProgrammableBlock, BoundingSphereD, Color, int, float, int, float, bool, int> _sphere;

            public int DrawMatrix(MatrixD matrix, float length = 1f, float thickness = DefaultThickness, float seconds = DefaultSeconds, bool? onTop = null) => _matrix?.Invoke(_pb, matrix, length, thickness, seconds, onTop ?? _defaultOnTop) ?? -1;
            Func<IMyProgrammableBlock, MatrixD, float, float, float, bool, int> _matrix;

            public int DrawGPS(string name, Vector3D origin, Color? color = null, float seconds = DefaultSeconds) => _gps?.Invoke(_pb, name, origin, color, seconds) ?? -1;
            Func<IMyProgrammableBlock, string, Vector3D, Color?, float, int> _gps;

            public int PrintHUD(string message, Font font = Font.Debug, float seconds = 2) => _printHUD?.Invoke(_pb, message, font.ToString(), seconds) ?? -1;
            Func<IMyProgrammableBlock, string, string, float, int> _printHUD;

            public void PrintChat(string message, string sender = null, Color? senderColor = null, Font font = Font.Debug) => _chat?.Invoke(_pb, message, sender, senderColor, font.ToString());
            Action<IMyProgrammableBlock, string, string, Color?, string> _chat;

            public void DeclareAdjustNumber(out int id, double initial, double step = 0.05, Input modifier = Input.Control, string label = null) => id = _adjustNumber?.Invoke(_pb, initial, step, modifier.ToString(), label) ?? -1;
            Func<IMyProgrammableBlock, double, double, string, string, int> _adjustNumber;

            public double GetAdjustNumber(int id, double noModDefault = 1) => _getAdjustNumber?.Invoke(_pb, id) ?? noModDefault;
            Func<IMyProgrammableBlock, int, double> _getAdjustNumber;

            public int GetTick() => _tick?.Invoke() ?? -1;
            Func<int> _tick;

            public enum Style { Solid, Wireframe, SolidAndWireframe }
            public enum Input { MouseLeftButton, MouseRightButton, MouseMiddleButton, MouseExtraButton1, MouseExtraButton2, LeftShift, RightShift, LeftControl, RightControl, LeftAlt, RightAlt, Tab, Shift, Control, Alt, Space, PageUp, PageDown, End, Home, Insert, Delete, Left, Up, Right, Down, D0, D1, D2, D3, D4, D5, D6, D7, D8, D9, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9, Multiply, Add, Separator, Subtract, Decimal, Divide, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12 }
            public enum Font { Debug, White, Red, Green, Blue, DarkBlue }

            const float DefaultThickness = 0.02f;
            const float DefaultSeconds = -1;

            IMyProgrammableBlock _pb;
            bool _defaultOnTop;

            public DebugAPI(MyGridProgram program, bool drawOnTopDefault = false)
            {
                if (program == null)
                    throw new Exception("Pass `this` into the API, not null.");

                _defaultOnTop = drawOnTopDefault;
                _pb = program.Me;

                var methods = _pb.GetProperty("DebugAPI")?.As<IReadOnlyDictionary<string, Delegate>>()?.GetValue(_pb);
                if (methods != null)
                {
                    Assign(out _removeAll, methods["RemoveAll"]);
                    Assign(out _removeDraw, methods["RemoveDraw"]);
                    Assign(out _remove, methods["Remove"]);
                    Assign(out _point, methods["Point"]);
                    Assign(out _line, methods["Line"]);
                    Assign(out _aabb, methods["AABB"]);
                    Assign(out _obb, methods["OBB"]);
                    Assign(out _sphere, methods["Sphere"]);
                    Assign(out _matrix, methods["Matrix"]);
                    Assign(out _gps, methods["GPS"]);
                    Assign(out _printHUD, methods["HUDNotification"]);
                    Assign(out _chat, methods["Chat"]);
                    Assign(out _adjustNumber, methods["DeclareAdjustNumber"]);
                    Assign(out _getAdjustNumber, methods["GetAdjustNumber"]);
                    Assign(out _tick, methods["Tick"]);
                    RemoveAll();
                    ModDetected = true;
                }
            }

            void Assign<T>(out T field, object method) => field = (T)method;
        }
    }

    public class CubeMesh
    {
        public Matrix Rotation;
        private SortedSet<Vector3> _vertices = new SortedSet<Vector3>();
        public Vector3[] Vertices
        {
            get
            {
                return _vertices.ToArray();
            }
            set
            {
                _vertices.Clear();
                foreach (var item in value)
                {
                    _vertices.Add(item);
                }
                Min = _vertices.First();
                Max = _vertices.Last();
            }
        }
        public Vector3 Min;
        public Vector3 Max;

        public bool PointIntersects(Vector3 pos)
        {
            return IsBigger(pos, Min) && IsSmaller(pos, Max);
        }

        public bool IsBigger(Vector3 v1, Vector3 v2)
        {
            return v1.X > v2.X && v1.Y > v2.Y && v1.Z > v2.Z;
        }

        public bool IsSmaller(Vector3 v1, Vector3 v2)
        {
            return v1.X < v2.X && v1.Y < v2.Y && v1.Z < v2.Z;
        }
    }

    public class LCDWithRotation
    {
        public IMyTextPanel TextPanel;
        public Vector3 Rotation;
    }

    public class GridBlock
    {
        public Vector3 Position { get; set; }
        public IMySlimBlock SlimBlock { get; set; }
        public IMyTerminalBlock TerminalBlock { get; set; }
        private int LastMeshHash { get; set; }
        private CubeMesh _mesh;
        private Matrix rot;
        public CubeMesh Mesh(Matrix rotation)
        {
            if (LastMeshHash == GetHashCode() || rot != rotation)
            {
                _mesh = new CubeMesh();
                _mesh.Rotation = rotation;
                _mesh.Vertices = Vertices.ToArray();
                rot = rotation;
            }
            return _mesh;
        }
        public bool Exists { get; set; }
        public bool IsOutside;

        public static readonly float BlockSize = 2.5f;
        public static readonly float BlockSizeHalf = BlockSize / 2f;

        public bool IsAir
        {
            get
            {
                return !Exists;
            }
        }

        public GridBlockStatus Status
        {
            get
            {
                return SlimBlock.IsFullIntegrity ?
                    GridBlockStatus.Intact :
                    (TerminalBlock != null ?
                        (TerminalBlock.IsFunctional ?
                            (GridBlockStatus.Intact | GridBlockStatus.Damaged) : GridBlockStatus.NotWorking) :
                            (SlimBlock.IsDestroyed ?
                                GridBlockStatus.Missing :
                                GridBlockStatus.Damaged));
            }
        }

        public static List<Vector3> NeighbourBlocksGrid = new List<Vector3>()
        {
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 0, 1),
            new Vector3(-1, 0, 0),
            new Vector3(0, -1, 0),
            new Vector3(0, 0, -1)
        };

        public static List<Vector3> Vertices = new List<Vector3>() {
            new Vector3(BlockSizeHalf, BlockSizeHalf, BlockSizeHalf),
            new Vector3(-BlockSizeHalf, BlockSizeHalf, BlockSizeHalf),
            new Vector3(BlockSizeHalf, -BlockSizeHalf, BlockSizeHalf),
            new Vector3(BlockSizeHalf, BlockSizeHalf, -BlockSizeHalf),
            new Vector3(-BlockSizeHalf, -BlockSizeHalf, BlockSizeHalf),
            new Vector3(-BlockSizeHalf, -BlockSizeHalf, -BlockSizeHalf),
            new Vector3(BlockSizeHalf, -BlockSizeHalf, -BlockSizeHalf),
            new Vector3(-BlockSizeHalf, BlockSizeHalf, -BlockSizeHalf)
        };

        public enum GridBlockStatus
        {
            Intact = 0 ^ 2,
            Damaged = 1 ^ 2,
            NotWorking = 2 ^ 2,
            Missing = 3 ^ 2
        }

        public override int GetHashCode()
        {
            return Position.GetHashCode();
        }
    }
}
