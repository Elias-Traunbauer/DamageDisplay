﻿using Sandbox.Game.EntityComponents;
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
        readonly SEUtils _seu;
        readonly MyIni _ini;
        Dictionary<Vector3, GridBlock> _gridBlocks;
        readonly Dictionary<TextPanelRenderingContext, List<LCDWithRotation>> _textPanelRenderingContexts = new Dictionary<TextPanelRenderingContext, List<LCDWithRotation>>();
        //MySprite _pixelSpriteTemplate = new MySprite()
        //{
        //    Data = "SquareSimple",
        //    Color = Color.White,
        //    Size = new Vector2(1, 1)
        //};
        int _iterationCount = 0;
        float _tempRotElevation = 0;
        float _tempRotAzimuth = 0;
        readonly int _maxIterationCount = 100;
        readonly string _currentDisplayId = "main";
        const string INI_SECTION_HEADER = "Damage Display";
        const string INI_SECTION_COMMENT = "Configuration for damage display controller";
        const string SELECTION_KEYWORD = "damage_display";
        const string DISPLAY_ID_KEY = "Display_ID";
        const string DISPLAY_ID_DEFAULT = "main";
        const string END_COMMENT = "The PB with a certain ID displays only on LCD's with that ID.\nPlease configure";
        const float TRIANGLE_SIZE_MULITPLIER = 0.3f;
        Mesh _shipMesh;

        public Program()
        {
            _seu = new SEUtils(this);
            _ini = new MyIni();

            // check if PB has valid ini config
            string pbCustomData = _seu.CurrentProgrammableBlock.CustomData;
            if (_ini.TryParse(pbCustomData) && !string.IsNullOrEmpty(pbCustomData))
            {
                if (_ini.ContainsSection(INI_SECTION_HEADER))
                {
                    _currentDisplayId = _ini.Get(INI_SECTION_HEADER, DISPLAY_ID_KEY).ToString();
                }
            }
            else
            {
                _ini.AddSection(INI_SECTION_HEADER);
                _ini.SetSectionComment(INI_SECTION_HEADER, INI_SECTION_COMMENT);
                _ini.Set(INI_SECTION_HEADER, DISPLAY_ID_KEY, DISPLAY_ID_DEFAULT);
                _ini.EndComment = END_COMMENT;
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
                        string ID = _ini.Get(INI_SECTION_HEADER, DISPLAY_ID_KEY).ToString();
                        vectorString = vectorString.Substring(1, vectorString.Length - 2);
                        string[] numbers = vectorString.Split(' ').Select(x => x.Split(':')[1]).ToArray();
                        if (ID == _currentDisplayId)
                        {
                            lcds.Add(new LCDWithRotation() { TextPanel = item, Rotation = new Vector3(MathHelper.ToRadians(float.Parse(numbers[0])), MathHelper.ToRadians(float.Parse(numbers[1])), MathHelper.ToRadians(float.Parse(numbers[2]))) });
                        }
                    }
                }
                else if (item.CustomData.ToLower().Contains(SELECTION_KEYWORD))
                {
                    _ini.AddSection(INI_SECTION_HEADER);
                    _ini.SetSectionComment(INI_SECTION_HEADER, INI_SECTION_COMMENT);
                    _ini.Set(INI_SECTION_HEADER, DISPLAY_ID_KEY, DISPLAY_ID_DEFAULT);
                    _ini.Set(INI_SECTION_HEADER, "Display_type", "3D");
                    _ini.SetComment(INI_SECTION_HEADER, "Display_type", "Options: [3D|2D]");
                    _ini.Set(INI_SECTION_HEADER, "View_rotation", new Vector3().ToString());
                    item.CustomData = _ini.ToString();
                }
                _ini.Clear();
            }

            var uniqueLcdTypes = lcds.GroupBy(x => x.TextPanel.BlockDefinition.SubtypeId + x.Rotation.ToString());
            _textPanelRenderingContexts = uniqueLcdTypes.Select(x => new { CTX = new TextPanelRenderingContext(x.First().TextPanel, Vector3D.Backward), LCDs = x.ToList() }).ToDictionary(x => x.CTX, y => y.LCDs);
            allLCDsOnGrid = null;

            foreach (var textPanelRenderingContexts in _textPanelRenderingContexts.SelectMany(x => x.Value))
            {
                textPanelRenderingContexts.TextPanel.ContentType = ContentType.SCRIPT;
                textPanelRenderingContexts.TextPanel.ScriptBackgroundColor = Color.Black;
            }

            _seu.StartCoroutine(StartSystem());
        }

        public void Main(string argument, UpdateType updateSource)
        {
            _iterationCount = 0;
            if (!_seu.RuntimeUpdate(argument, updateSource)) return;
        }

        private void DrawLogo(MySpriteDrawFrame df, IMyTextSurface lcd)
        {
            df.Add(new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = "trauni's\ndamage\ndisplay",
                FontId = "Monospace",
                Position = new Vector2(lcd.SurfaceSize.X / 2, 10),
                RotationOrScale = 2,
                Alignment = TextAlignment.CENTER
            });
        }

        IEnumerator StartSystem()
        {
            int id = _seu.StartCoroutine(GatherInitialShipInfo());
            //yield return new WaitForNextTick();
            yield return new WaitForConditionMet(() => !_seu.CheckCoroutineRunning(id), 120000, 1000, () => { Echo("Script start timeouted"); _seu.StopCoroutine(id); return false; });
            // start main coroutine
            _seu.StartCoroutine(Main());
        }

        IEnumerator GatherInitialShipInfo()
        {
            var origin = _seu.CurrentCubeGrid.Min + _seu.CurrentCubeGrid.Min.To(_seu.CurrentCubeGrid.Max) / 2;
            var lcd = _textPanelRenderingContexts.First().Value.First().TextPanel;
            var df = lcd.DrawFrame();

            yield return new WaitForNextTick();
            //try { throw new InvalidOperationException("break my point"); } catch (Exception) { }
            Echo("Started gathering all blocks");

            Vector3 gridSize = _seu.CurrentCubeGrid.Max - _seu.CurrentCubeGrid.Min;
            // maxFarOut * x = 2.4f -> 2.4f / maxFarOut = x
            float maxFarOut = gridSize.Max() / 2;
            _gridBlocks = new Dictionary<Vector3, GridBlock>();
            for (int x = 0; x < gridSize.X; x++)
            {
                for (int y = 0; y < gridSize.Y; y++)
                {
                    for (int z = 0; z < gridSize.Z; z++)
                    {
                        var pos = _seu.CurrentCubeGrid.Min + new Vector3I(x, y, z);
                        var slim = _seu.CurrentCubeGrid.GetCubeBlock(pos);

                        if (_seu.CurrentCubeGrid.CubeExists(pos)) 
                            _gridBlocks.Add(pos - origin, new GridBlock() { Position = slim != null ? slim.Position - origin : pos - origin, SlimBlock = slim, TerminalBlock = slim as IMyTerminalBlock, Exists = _seu.CurrentCubeGrid.CubeExists(pos) }); ;

                        _iterationCount++;
                        if (_iterationCount >= _maxIterationCount)
                        {
                            yield return new WaitForNextTick();
                        }
                    }
                }
            }
            Echo("Flood fill started");
            // flood fill to find outside blocks
            var all = gridSize.X * gridSize.Y * gridSize.Z - _gridBlocks.Count;
            float cnt = 0;
            Vector3 floodFillStartPos = _seu.CurrentCubeGrid.Min;
            HashSet<Vector3> outsidePoses = new HashSet<Vector3>();
            HashSet<Vector3> alreadyChecked = new HashSet<Vector3>();
            Queue<Vector3> queuePosToCheck = new Queue<Vector3>();
            queuePosToCheck.Enqueue(floodFillStartPos);
            while (queuePosToCheck.Any())
            {
                var posToCheck = queuePosToCheck.Dequeue();
                if (alreadyChecked.Any(x => x == posToCheck) || !(posToCheck.X >= _seu.CurrentCubeGrid.Min.X && posToCheck.Y >= _seu.CurrentCubeGrid.Min.Y && posToCheck.Z >= _seu.CurrentCubeGrid.Min.Z) && (posToCheck.X <= _seu.CurrentCubeGrid.Max.X && posToCheck.Y <= _seu.CurrentCubeGrid.Max.Y && posToCheck.Z <= _seu.CurrentCubeGrid.Max.Z))
                {
                    continue;
                }
                alreadyChecked.Add(posToCheck);
                cnt++;

                if (_gridBlocks.ContainsKey(posToCheck))
                {
                    outsidePoses.Add(posToCheck);
                }
                else
                {
                    Echo("Min: " + _seu.CurrentCubeGrid.Min + " Max: " + _seu.CurrentCubeGrid.Max + "");
                    foreach (var item in DirectNeighbours(posToCheck))
                    {
                        Echo("pos: " + item);
                        queuePosToCheck.Enqueue(item);
                        Echo(cnt + "/" + all);
                        Echo(alreadyChecked.Count() + " sdaadd");
                        _iterationCount += alreadyChecked.Count;
                        if (_iterationCount >= _maxIterationCount)
                        {
                            yield return new WaitForNextTick();
                            df = lcd.DrawFrame();
                            DrawLogo(df, lcd);
                            var surfSize = lcd.SurfaceSize;
                            df.Add(new MySprite()
                            {
                                Type = SpriteType.TEXTURE,
                                Data = "SquareSimple",
                                Position = new Vector2(15, surfSize.Y / 2),
                                Size = new Vector2(surfSize.X - 30, 32),
                                Color = Color.Gray
                            });
                            df.Add(new MySprite()
                            {
                                Type = SpriteType.TEXTURE,
                                Data = "SquareSimple",
                                Position = new Vector2(16, surfSize.Y / 2),
                                Size = new Vector2((surfSize.X - 32) * (cnt / all), 30),
                                Color = Color.Green
                            });
                            df.Dispose();
                        }
                    }
                }
                _iterationCount++;
                if (_iterationCount >= _maxIterationCount)
                {
                    Echo(cnt + "/" + all);
                    yield return new WaitForNextTick();
                    df = lcd.DrawFrame();
                    DrawLogo(df, lcd);
                    var surfSize = lcd.SurfaceSize;
                    df.Add(new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Data = "SquareSimple",
                        Position = new Vector2(15, surfSize.Y / 2),
                        Size = new Vector2(surfSize.X - 30, 32),
                        Color = Color.Gray
                    });
                    df.Add(new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Data = "SquareSimple",
                        Position = new Vector2(16, surfSize.Y / 2),
                        Size = new Vector2((surfSize.X - 32) * (cnt / all), 30),
                        Color = Color.Green
                    });
                    df.Dispose();
                }
            }
            
            Dictionary<Vector3, int> vertices = new Dictionary<Vector3, int>();
            HashSet<Triangle> triangles = new HashSet<Triangle>();
            
            cnt = 0;
            //var rendaBlocks = _gridBlocks.Where(x => x.Value.Exists);
            all = outsidePoses.Count();
            int indexOfVertex = 0;
            foreach (var item in outsidePoses)
            {
                cnt++;
                int[] vertexIndices = new int[GridBlock.ExampleCubeVertices.Count()];
                for (int i = 0; i < GridBlock.ExampleCubeVertices.Count; i++)
                {
                    var vertex = GridBlock.ExampleCubeVertices[i] + item;
                    
                    if (!vertices.ContainsKey(vertex))
                    {
                        vertices.Add(vertex, indexOfVertex);
                        indexOfVertex++;
                    }
                    vertexIndices[i] = vertices[vertex];

                    _iterationCount += vertices.Count;
                    if (_iterationCount >= _maxIterationCount)
                    {
                        yield return new WaitForNextTick();
                        df = lcd.DrawFrame();
                        DrawLogo(df, lcd);
                        var surfSize = lcd.SurfaceSize;
                        df.Add(new MySprite()
                        {
                            Type = SpriteType.TEXTURE,
                            Data = "SquareSimple",
                            Position = new Vector2(15, surfSize.Y / 2),
                            Size = new Vector2(surfSize.X - 30, 32),
                            Color = Color.Gray
                        });
                        df.Add(new MySprite()
                        {
                            Type = SpriteType.TEXTURE,
                            Data = "SquareSimple",
                            Position = new Vector2(16, surfSize.Y / 2),
                            Size = new Vector2((surfSize.X - 32) * (cnt / all), 30),
                            Color = Color.Green
                        });
                        df.Dispose();
                    }
                }
                for (int t = 0; t < GridBlock.ExampleCubeTriangles.Length; t+=3)
                {
                    var triangle = new Triangle() { vertices = new int[] { vertexIndices[GridBlock.ExampleCubeTriangles[t]], vertexIndices[GridBlock.ExampleCubeTriangles[t + 1]], vertexIndices[GridBlock.ExampleCubeTriangles[t + 2]] } };

                    triangles.Add(triangle);

                    _iterationCount+=triangles.Count;
                    if (_iterationCount >= _maxIterationCount)
                    {
                        yield return new WaitForNextTick();
                        df = lcd.DrawFrame();
                        DrawLogo(df, lcd);
                        var surfSize = lcd.SurfaceSize;
                        df.Add(new MySprite()
                        {
                            Type = SpriteType.TEXTURE,
                            Data = "SquareSimple",
                            Position = new Vector2(15, surfSize.Y / 2),
                            Size = new Vector2(surfSize.X - 30, 32),
                            Color = Color.Gray
                        });
                        df.Add(new MySprite()
                        {
                            Type = SpriteType.TEXTURE,
                            Data = "SquareSimple",
                            Position = new Vector2(16, surfSize.Y / 2),
                            Size = new Vector2((surfSize.X - 32) * (cnt / all), 30),
                            Color = Color.Green
                        });
                        df.Dispose();
                    }
                }
            }
            _shipMesh = new Mesh
            {
                Vertices = vertices.OrderBy(x => x.Value).Select(x => x.Key).ToArray(),
                Triangles = triangles.SelectMany(x => x.vertices).ToArray()
            };
            Echo("Gathered all blocks");
        }


        IEnumerator Main()
        {
            while (true)
            {
                Echo("Rendering...");
                _tempRotElevation += 0.02f;
                _tempRotElevation = MathHelper.WrapAngle(_tempRotElevation); 
                _tempRotAzimuth += 0.005f;
                _tempRotAzimuth = MathHelper.WrapAngle(_tempRotAzimuth);
                Mesh m = _shipMesh;
                m.Color = Color.White;
                m.Rotation = Matrix.CreateRotationY(_tempRotElevation) * Matrix.CreateRotationX(_tempRotAzimuth * 0);
                
                Vector3 forward = Vector3.Forward * m.Vertices.Select(x => x.Length()).Max() * 2f;
                float[] colorTriangles = new float[m.Triangles.Length / 3];
                bool yes = true;
                float rate = 235 / colorTriangles.Length / 2;
                float color = 20;
                for (int i = 0; i < colorTriangles.Length; i++)
                {
                    if (yes)
                    {
                        color += rate;
                    }
                    yes = !yes;
                    colorTriangles[i] = color;
                }
                Echo($"Rendering {m.Vertices.Count()} vertices");
                foreach (var context in _textPanelRenderingContexts)
                {
                    var multi = context.Key.LCDBlockSize.X > context.Key.LCDBlockSize.Y ? context.Key.LCDBlockSize.Y : context.Key.LCDBlockSize.X;
                    multi *= 2;
                    foreach (var lcd in context.Value)
                    {
                        var df = lcd.TextPanel.DrawFrame();

                        MyTuple<int, float, float>[] triangleDistances = new MyTuple<int, float, float>[m.Triangles.Length/3];
                        for (int i = 0; i < m.Triangles.Length; i+=3)
                        {
                            triangleDistances[i / 3] = new MyTuple<int, float, float>(i, new Vector3((m.GetAt(m.Triangles[i]) * multi + forward).LengthSquared(), (m.GetAt(m.Triangles[i+1]) * multi + forward).LengthSquared(), (m.GetAt(m.Triangles[i+2]) * multi + forward).LengthSquared()).Max(), colorTriangles[i / 3]);
                            _iterationCount++;
                            if (_iterationCount >= _maxIterationCount)
                            {
                                yield return new WaitForNextTick();
                            }
                        }
                        triangleDistances = triangleDistances.OrderByDescending(x => x.Item2).ToArray();
                        for (int i = 0; i < triangleDistances.Length; i++)
                        {
                            float w = triangleDistances[i].Item3;
                            var renderedVertices = new Vector2?[3];

                            for (int v = 0; v < 3; v++)
                            {
                                renderedVertices[v] = context.Key.ProjectLocalPoint(m.GetAt(m.Triangles[triangleDistances[i].Item1 + v]) + forward);
                            }

                            if (!renderedVertices.Any(x => x == null))
                            {
                                var screenPoints = renderedVertices.Select(x => (Vector2)x).ToArray();
                                if (GetPointsOrientation(screenPoints[0], screenPoints[1], screenPoints[2]) == 2)
                                {
                                    FillArbitraryTriangle(screenPoints[0], screenPoints[1], screenPoints[2], Color.FromNonPremultiplied((int)w, (int)w, (int)w, 255), df);
                                }
                            }

                            _iterationCount++;
                            if (_iterationCount >= _maxIterationCount)
                            {
                                yield return new WaitForNextTick();
                            }
                        }
                        df.Dispose();
                        Echo($"Rendered {m.Vertices.Count()} vertices onto {lcd.TextPanel.CustomName}");
                    }
                }

                yield return new WaitForNextTick();
            }
        }

        // To find orientation of ordered triplet
        // (p1, p2, p3). The function returns
        // following values
        // 0 --> line
        // 1 --> Clockwise
        // 2 --> Counterclockwise
        public static int GetPointsOrientation(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            int val = (int)((p2.Y - p1.Y) * (p3.X - p2.X) -
                    (p2.X - p1.X) * (p3.Y - p2.Y));

            if (val == 0) return 0; // collinear

            // clock or counterclock wise
            return (val > 0) ? 1 : 2;
        }

        float GetRotation(Vector2 dir)
        {
            float az, el;
            Vector3.GetAzimuthAndElevation(new Vector3(-dir.X, 0, dir.Y), out az, out el);
            return az;
        }

        //MySprite DrawLine(Vector2 start, Vector2 end, Color c)
        //{
        //    MySprite sp = new MySprite
        //    {
        //        Color = c,
        //        Type = SpriteType.TEXTURE,
        //        Data = "SquareSimple"
        //    };
        //    Vector2 dir = end - start;
        //    float len = dir.Length();
        //    Vector2 size = new Vector2(2f, len);
        //    dir.Normalize();
        //    sp.Position = start + dir * (len / 2);
        //    sp.Size = size;
        //    float az, el;
        //    Vector3.GetAzimuthAndElevation(new Vector3(-dir.X, 0, dir.Y), out az, out el);
        //    sp.RotationOrScale = az;
        //    return sp;
        //}

        void FillArbitraryTriangle(Vector2 p1, Vector2 p2, Vector2 p3, Color c, MySpriteDrawFrame df)
        {
            MySprite sp = new MySprite
            {
                Type = SpriteType.TEXTURE,
                Data = "RightTriangle",
                Color = c
            };

            // make triangle larger to beat the evil ugly lines
            var pointBetweenP1andP2 = p1 + p1.To(p2) / 2;
            var pointBetweenP1andP3 = p1 + p1.To(p3) / 2;
            var pointBetweenP2andP3 = p2 + p2.To(p3) / 2;
            p1 += Vector2.Normalize(pointBetweenP2andP3.To(p1)) * TRIANGLE_SIZE_MULITPLIER;
            p2 += Vector2.Normalize(pointBetweenP1andP3.To(p2)) * TRIANGLE_SIZE_MULITPLIER;
            p3 += Vector2.Normalize(pointBetweenP1andP2.To(p3)) * TRIANGLE_SIZE_MULITPLIER;

            // 1st line: p1->p2
            // 2nd line: p2->p3
            // 3rd line: p1->p3

            // find the hypotenuse and assign the correct values
            float[] lineLengths = new float[] { Vector2.Distance(p1, p2), Vector2.Distance(p2, p3), Vector2.Distance(p1, p3) };

            // A->B = hypotenuse

            Vector2 A;
            Vector2 B;
            Vector2 AtoB;
            Vector2 AtoBNormalized;
            Vector2 C;
            float maxLineLength = lineLengths.Max();

            if (maxLineLength == lineLengths[0])
            {
                // 1st line hyp
                A = p1;
                B = p2;
                C = p3;
            }
            else if (maxLineLength == lineLengths[1])
            {
                // 2nd line hyp
                A = p2;
                B = p3;
                C = p1;
            }
            else
            {
                // 3rd line hyp
                A = p3;
                B = p1;
                C = p2;
            }
            // general setup
            AtoB = A.To(B);
            AtoBNormalized = Vector2.Normalize(AtoB);
            // anchor for the two right angled triangles
            Vector2 triangleAnchor;
            Vector2 AtoC = A.To(C);
            float dotProduct = Vector2.Dot(AtoC, AtoBNormalized);
            triangleAnchor = A + AtoBNormalized * dotProduct;
            float baseRot = MathHelper.Pi / 2;

            // first triangle
            Vector2 AtoTriangleAnchor = A.To(triangleAnchor);
            // magic vector to make ugly line between the two triangles, drawn here, disappear
            Vector2 localLineEliminatorC = C + Vector2.Normalize(AtoTriangleAnchor) / 2/*+ Vector2.Normalize(triangleAnchor.To(C)) * 1.5f*/;
            float rotation = GetRotation(AtoTriangleAnchor) + baseRot;
            Vector2 lineEliminatorA = A + Vector2.Normalize(triangleAnchor.To(A)) * 1.5f;
            Vector2 spriteSize = lineEliminatorA.To(localLineEliminatorC) + Vector2.Normalize(triangleAnchor.To(C));
            spriteSize.Rotate(-(GetRotation(AtoTriangleAnchor) + baseRot));
            spriteSize *= -1;
            sp.Size = spriteSize;
            Vector2 spritePosition = lineEliminatorA + lineEliminatorA.To(localLineEliminatorC) / 2 - new Vector2(spriteSize.X / 2, 0);
            sp.Position = spritePosition;
            sp.RotationOrScale = rotation;
            df.Add(sp);

            // second triangle, basically like before, but now mirrored
            localLineEliminatorC = C + Vector2.Normalize(B.To(triangleAnchor)) / 2/* + Vector2.Normalize(triangleAnchor.To(C)) * 1.5f*/;
            rotation = GetRotation(AtoTriangleAnchor) + baseRot;
            Vector2 lineEliminatorB = B + Vector2.Normalize(triangleAnchor.To(B)) * 1.5f;
            Vector2 BtoC = lineEliminatorB.To(localLineEliminatorC);
            var BtoTriangleAnchor = lineEliminatorB.To(triangleAnchor);
            spriteSize = BtoC + Vector2.Normalize(triangleAnchor.To(C));
            spriteSize.Rotate(-(GetRotation(AtoTriangleAnchor) + baseRot));
            spriteSize *= -1;
            sp.Size = spriteSize;
            spritePosition = lineEliminatorB + triangleAnchor.To(localLineEliminatorC) / 2 + BtoTriangleAnchor / 2 - new Vector2(spriteSize.X / 2, 0);
            sp.Position = spritePosition;
            sp.RotationOrScale = rotation;
            df.Add(sp);
        }

        //void DrawPixel(Vector2? pos, Color c, MySpriteDrawFrame df)
        //{
        //    if (pos != null)
        //    {
        //        _pixelSpriteTemplate.Position = pos;
        //        var tmp = _pixelSpriteTemplate.Color;
        //        _pixelSpriteTemplate.Color = c;
        //        df.Add(_pixelSpriteTemplate);
        //        _pixelSpriteTemplate.Color = tmp;
        //    }
        //}

        IEnumerable<Vector3> DirectNeighbours(Vector3 pos)
        {
            yield return pos + Vector3.Forward;
            yield return pos + Vector3.Backward;
            yield return pos + Vector3.Right;
            yield return pos + Vector3.Left;
            yield return pos + Vector3.Up;
            yield return pos + Vector3.Down;
        }

        public void Save()
        {

        }
    }

    public class Vertex
    {
        public Vector3 Position;
        public int Index;

        public Vertex(Vector3 position, int index)
        {
            Position = position;
            Index = index;
        }

        public override int GetHashCode()
        {
            return Position.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Vertex))
            {
                throw new ArgumentException($"{nameof(obj)} has to be instance of {nameof(Vertex)}");
            }
            return Position.Equals((obj as Vertex).Position);
        }

        public override string ToString()
        {
            return Position.ToString() + " Index: " + Index;
        }
    }

    public class Mesh
    {
        public Color Color { get; set; }
        private Vector3[] _vertices;

        public Vector3[] Vertices
        {
            get
            {
                return _vertices;
            }
            set { _vertices = value; }
        }

        public Vector3 GetAt(int i)
        {
            return Vector3.Transform(_vertices[i], _rotation);
        }

        private int[] _triangles;

        public int[] Triangles
        {
            get { return _triangles; }
            set { _triangles = value; }
        }

        private Matrix _rotation;

        public Matrix Rotation
        {
            get { return _rotation; }
            set
            {
                _rotation = value;
            }
        }
    }

    public class Triangle
    {
        public int[] vertices;
        // compares two of the 3 vertices
        public override bool Equals(object obj)
        {
            int firstCounter = -1;
            int secondCounter = -1;
            return vertices.OrderBy(x => x).Aggregate((x, y) => { firstCounter += 2; return x * firstCounter + y * firstCounter + 1; }) == (obj as Triangle).vertices.OrderBy(x => x).Aggregate((x, y) => { secondCounter += 2; return x * secondCounter + y * secondCounter + 1; });
        }

        public override int GetHashCode()
        {
            return 911371710 + EqualityComparer<int[]>.Default.GetHashCode(vertices.OrderBy(x => x).ToArray());
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
        private Mesh _mesh;
        private Matrix rot;
        public Mesh Mesh(Matrix rotation)
        {
            if (LastMeshHash == GetHashCode() || rot != rotation)
            {
                _mesh = new Mesh
                {
                    Rotation = rotation,
                    Vertices = ExampleCubeVertices.ToArray()
                };
                rot = rotation;
            }
            return _mesh;
        }
        public bool Exists { get; set; }
        public bool IsOutside;

        public static readonly float BlockSize = 1f;
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

        public static List<Vector3> ExampleCubeVertices = new List<Vector3>() {
            new Vector3(BlockSizeHalf, BlockSizeHalf, BlockSizeHalf),    // 0
            new Vector3(-BlockSizeHalf, BlockSizeHalf, BlockSizeHalf),   // 1
            new Vector3(BlockSizeHalf, -BlockSizeHalf, BlockSizeHalf),   // 2
            new Vector3(BlockSizeHalf, BlockSizeHalf, -BlockSizeHalf),   // 3
            new Vector3(-BlockSizeHalf, -BlockSizeHalf, BlockSizeHalf),  // 4
            new Vector3(-BlockSizeHalf, -BlockSizeHalf, -BlockSizeHalf), // 5
            new Vector3(BlockSizeHalf, -BlockSizeHalf, -BlockSizeHalf),  // 6
            new Vector3(-BlockSizeHalf, BlockSizeHalf, -BlockSizeHalf)   // 7
        };

        public static int[] ExampleCubeTriangles = new int[]
        {
            0,2,4,
            4,1,0,
            2,0,3,
            3,6,2,
            3,7,5,
            5,6,3,
            7,1,4,
            4,5,7,
            6,5,4,
            4,2,6,
            0,1,7,
            7,3,0
        };

        public static List<Vector3> ExampleCubeSVertices = new List<Vector3>() {
            new Vector3(BlockSizeHalf, BlockSizeHalf * 0.2f, BlockSizeHalf),    // 0
            new Vector3(-BlockSizeHalf, BlockSizeHalf * 0.2f, BlockSizeHalf),   // 1
            new Vector3(BlockSizeHalf, -BlockSizeHalf, BlockSizeHalf),   // 2
            new Vector3(BlockSizeHalf, BlockSizeHalf * 0.2f, -BlockSizeHalf),   // 3
            new Vector3(-BlockSizeHalf, -BlockSizeHalf, BlockSizeHalf),  // 4
            new Vector3(-BlockSizeHalf, -BlockSizeHalf, -BlockSizeHalf), // 5
            new Vector3(BlockSizeHalf, -BlockSizeHalf, -BlockSizeHalf),  // 6
            new Vector3(-BlockSizeHalf, BlockSizeHalf * 2, -BlockSizeHalf)   // 7
        };

        public static int[] ExampleCubeSTriangles = new int[]
        {
            0,2,4,
            4,1,0,
            2,0,3,
            3,6,2,
            3,7,5,
            5,6,3,
            7,1,4,
            4,5,7,
            6,5,4,
            4,2,6,
            0,1,7,
            7,3,0
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
