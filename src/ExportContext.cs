//The MIT License (MIT)

//Those portions created by va3c authors are provided with the following copyright:

//Copyright (c) 2014 va3c

//Those portions created by Thornton Tomasetti employees are provided with the following copyright:

//Copyright (c) 2015 Thornton Tomasetti

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

#region Namespaces

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Newtonsoft.Json;

#endregion // Namespaces

namespace RvtVa3c
{
    // Done:
    // Check instance transformation
    // Support transparency
    // Add scaling for Theo [(0,0),(20000,20000)]
    // Implement the external application button
    // Implement element properties
    // Eliminate multiple materials 
    // Prompt user for output file name and location
    // Eliminate null element properties, i.e. useless 
    //     JSON userData entries
    // TODO:
    // Check for file size
    // Instance/type reuse

    public class ExportContext : IExportContext
    {
        /// <summary>
        /// Scale entire top level BIM object node in JSON
        /// output. A scale of 1.0 will output the model in 
        /// millimeters. Currently we scale it to decimeters
        /// so that a typical model has a chance of fitting 
        /// into a cube with side length 100, i.e. 10 meters.
        /// </summary>
        readonly double _scale_bim = 1.0;

        /// <summary>
        /// Scale applied to each vertex in each individual 
        /// BIM element. This can be used to scale the model 
        /// down from millimeters to meters, e.g.
        /// Currently we stick with millimeters after all
        /// at this level.
        /// </summary>
        readonly double _scale_vertex = 1.0;

        /// <summary>
        /// If true, switch Y and Z coordinate 
        /// and flip X to negative to convert from
        /// Revit coordinate system to standard 3d
        /// computer graphics coordinate system with
        /// Z pointing out of screen, X towards right,
        /// Y up.
        /// </summary>
        readonly bool _switchCoordinates = true;

        #region VertexLookupXyz

        #endregion // VertexLookupXyz

        #region VertexLookupInt

        /// <inheritdoc />
        /// <summary>
        /// An integer-based 3D point class.
        /// </summary>
        private class PointInt : IComparable<PointInt>
        {
            public long X { get; }
            public long Y { get; }
            public long Z { get; }

            //public PointInt( int x, int y, int z )
            //{
            //  X = x;
            //  Y = y;
            //  Z = z;
            //}

            /// <summary>
            /// Consider a Revit length zero 
            /// if is smaller than this.
            /// </summary>
            const double EPS = 1.0e-9;

            /// <summary>
            /// Conversion factor from feet to millimeters.
            /// </summary>
            const double FEET_TO_MM = 25.4 * 12;

            /// <summary>
            /// Conversion a given length value 
            /// from feet to millimeter.
            /// </summary>
            private static long ConvertFeetToMillimetres(double d)
            {
                if (0 < d)
                {
                    return EPS > d
                        ? 0
                        : (long) (FEET_TO_MM * d + 0.5);

                }
                return EPS > -d
                    ? 0
                    : (long) (FEET_TO_MM * d - 0.5);
            }

            public PointInt(XYZ p, bool switchCoordinates)
            {
                X = ConvertFeetToMillimetres(p.X);
                Y = ConvertFeetToMillimetres(p.Y);
                Z = ConvertFeetToMillimetres(p.Z);

                if (!switchCoordinates) return;
                X = -X;
                var tmp = Y;
                Y = Z;
                Z = tmp;
            }

            public int CompareTo(PointInt a)
            {
                var d = X - a.X;

                if (0 != d)
                    return (0 == d) ? 0 : ((0 < d) ? 1 : -1);

                d = Y - a.Y;

                if (0 == d)
                {
                    d = Z - a.Z;
                }
                return (0 == d) ? 0 : ((0 < d) ? 1 : -1);
            }
        }

        /// <inheritdoc />
        /// <summary>
        /// A vertex lookup class to eliminate 
        /// duplicate vertex definitions.
        /// </summary>
        private class VertexLookupInt : Dictionary<PointInt, int>
        {
            #region PointIntEqualityComparer

            /// <inheritdoc />
            /// <summary>
            /// Define equality for integer-based PointInt.
            /// </summary>
            private class PointIntEqualityComparer : IEqualityComparer<PointInt>
            {
                public bool Equals(PointInt p, PointInt q)
                {
                    return p != null && 0 == p.CompareTo(q);
                }

                public int GetHashCode(PointInt p)
                {
                    return (p.X + "," + p.Y + "," + p.Z).GetHashCode();
                }
            }

            #endregion // PointIntEqualityComparer

            public VertexLookupInt()
                : base(new PointIntEqualityComparer())
            {
            }

            /// <summary>
            /// Return the index of the given vertex,
            /// adding a new entry if required.
            /// </summary>
            public int AddVertex(PointInt p)
            {
                return ContainsKey(p)
                    ? this[p]
                    : this[p] = Count;
            }
        }

        #endregion // VertexLookupInt

        private Document _currentDoc;
        private readonly Document _doc;
        private readonly string _filename;
        private Container _container;

        private Dictionary<string, Container.Material> _materials;
        private Dictionary<string, Container.Object> _objects;
        private Dictionary<string, Container.Geometry> _geometries;
        private Dictionary<string, string> _viewsAndLayersDict;
        private List<string> _layerList;

        private Container.Object _currentElement;

        // Keyed on material uid to handle several materials per element:

        private Dictionary<string, Container.Object> _currentObject;
        private Dictionary<string, Container.Geometry> _currentGeometry;
        private Dictionary<string, VertexLookupInt> _vertices;

        private readonly Stack<ElementId> _elementStack = new Stack<ElementId>();
        private readonly Stack<Transform> _transformationStack = new Stack<Transform>();

        private string _currentMaterialUid;

        public string myjs;

        private Container.Object CurrentObjectPerMaterial => _currentObject[_currentMaterialUid];

        private Container.Geometry CurrentGeometryPerMaterial => _currentGeometry[_currentMaterialUid];

        private VertexLookupInt CurrentVerticesPerMaterial => _vertices[_currentMaterialUid];

        private Transform CurrentTransform => _transformationStack.Peek();

        public override string ToString()
        {
            return myjs;
        }

        /// <summary>
        /// Set the current material
        /// </summary>
        void SetCurrentMaterial(string uidMaterial)
        {
            if (!_materials.ContainsKey(uidMaterial))
            {
                if (_currentDoc.GetElement(uidMaterial) is Material material)
                {
                    var m = new Container.Material
                    {
                        uuid = uidMaterial,
                        name = material.Name,
                        type = "MeshLambertMaterial",
                        color = Util.ColorToInt(material.Color)
                    };

                    m.ambient = m.color;
                    m.emissive = 0;
                    m.opacity = 0.01 * (100 - material
                                            .Transparency
                                ); // Revit has material.Transparency in [0,100], three.js expects opacity in [0.0,1.0]
                    m.transparent = 0 < material.Transparency;
                    m.shading = 1;
                    m.wireframe = false;

                    _materials.Add(uidMaterial, m);
                }
            }
            _currentMaterialUid = uidMaterial;

            var uidPerMaterial = _currentElement.uuid + "-" + uidMaterial;

            if (!_currentObject.ContainsKey(uidMaterial))
            {
                Debug.Assert(!_currentGeometry.ContainsKey(uidMaterial), "expected same keys in both");

                _currentObject.Add(uidMaterial, new Container.Object());
                CurrentObjectPerMaterial.name = _currentElement.name;
                CurrentObjectPerMaterial.geometry = uidPerMaterial;
                CurrentObjectPerMaterial.material = _currentMaterialUid;
                CurrentObjectPerMaterial.matrix = new double[] {1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1};
                CurrentObjectPerMaterial.type = "Mesh";
                CurrentObjectPerMaterial.uuid = uidPerMaterial;
            }

            if (!_currentGeometry.ContainsKey(uidMaterial))
            {
                _currentGeometry.Add(uidMaterial, new Container.Geometry());
                CurrentGeometryPerMaterial.uuid = uidPerMaterial;
                CurrentGeometryPerMaterial.type = "Geometry";
                CurrentGeometryPerMaterial.data = new Container.GeometryData();
                CurrentGeometryPerMaterial.data.faces = new List<int>();
                CurrentGeometryPerMaterial.data.vertices = new List<double>();
                CurrentGeometryPerMaterial.data.normals = new List<double>();
                CurrentGeometryPerMaterial.data.uvs = new List<double>();
                CurrentGeometryPerMaterial.data.visible = true;
                CurrentGeometryPerMaterial.data.castShadow = true;
                CurrentGeometryPerMaterial.data.receiveShadow = false;
                CurrentGeometryPerMaterial.data.doubleSided = true;
                CurrentGeometryPerMaterial.data.scale = 1.0;
            }

            if (!_vertices.ContainsKey(uidMaterial))
            {
                _vertices.Add(uidMaterial, new VertexLookupInt());
            }
        }

        public ExportContext(Document document, string filename)
        {
            _doc = document;
            _currentDoc = document;
            _filename = filename;
        }

        public bool Start()
        {
            _materials = new Dictionary<string, Container.Material>();
            _geometries = new Dictionary<string, Container.Geometry>();
            _objects = new Dictionary<string, Container.Object>();

            _viewsAndLayersDict = new Dictionary<string, string>();
            _layerList = new List<string>();

            _transformationStack.Push(Transform.Identity);

            _container = new Container
            {
                metadata = new Container.Metadata
                {
                    type = "Object",
                    version = 4.3,
                    generator = "Spectacles.RevitExporter Revit Spectacles exporter"
                },
                geometries = new List<Container.Geometry>(),
                obj = new Container.Object
                {
                    uuid = _currentDoc.ActiveView.UniqueId,
                    name = "BIM " + _currentDoc.Title,
                    type = "Scene",
                    matrix = new[]
                    {
                        _scale_bim, 0, 0, 0,
                        0, _scale_bim, 0, 0,
                        0, 0, _scale_bim, 0,
                        0, 0, 0, _scale_bim
                    }
                }
            };

            return true;
        }

        public void Finish()
        {
            // Finish populating scene
            _container.materials = _materials.Values.ToList();
            _container.geometries = _geometries.Values.ToList();
            _container.obj.children = _objects.Values.ToList();

            if (Command.cameraNames.Count > 0)
            {
                //create an empty string to append the list of views
                var viewList = Command.cameraNames[0] + "," + Command.cameraPositions[0] + "," +
                               Command.cameraTargets[0];
                for (var i = 1; i < Command.cameraPositions.Count; i++)
                {
                    viewList += "," + Command.cameraNames[i] + "," + Command.cameraPositions[i] + "," +
                                Command.cameraTargets[i];
                }
                _viewsAndLayersDict.Add("views", viewList);
            }

            _container.obj.userData = _viewsAndLayersDict;

            // Serialise scene

            //using( FileStream stream
            //  = File.OpenWrite( filename ) )
            //{
            //  DataContractJsonSerializer serialiser
            //    = new DataContractJsonSerializer(
            //      typeof( SpectaclesContainer ) );
            //  serialiser.WriteObject( stream, _container );
            //}

            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            myjs = JsonConvert.SerializeObject(_container, Formatting.None, settings);

            File.WriteAllText(_filename, myjs);
        }

        public void OnPolymesh(PolymeshTopology polymesh)
        {
            //Debug.WriteLine( string.Format(
            //  "    OnPolymesh: {0} points, {1} facets, {2} normals {3}",
            //  polymesh.NumberOfPoints,
            //  polymesh.NumberOfFacets,
            //  polymesh.NumberOfNormals,
            //  polymesh.DistributionOfNormals ) );

            var pts = polymesh.GetPoints();
            var t = CurrentTransform;

            pts = pts.Select(p => t.OfPoint(p)).ToList();

            foreach (var facet in polymesh.GetFacets())
            {
                //Debug.WriteLine( string.Format(
                //  "      {0}: {1} {2} {3}", i++,
                //  facet.V1, facet.V2, facet.V3 ) );

                var v1 = CurrentVerticesPerMaterial.AddVertex(new PointInt(pts[facet.V1], _switchCoordinates));
                var v2 = CurrentVerticesPerMaterial.AddVertex(new PointInt(pts[facet.V2], _switchCoordinates));
                var v3 = CurrentVerticesPerMaterial.AddVertex(new PointInt(pts[facet.V3], _switchCoordinates));

                CurrentGeometryPerMaterial.data.faces.Add(0);
                CurrentGeometryPerMaterial.data.faces.Add(v1);
                CurrentGeometryPerMaterial.data.faces.Add(v2);
                CurrentGeometryPerMaterial.data.faces.Add(v3);
            }
        }

        public void OnMaterial(MaterialNode node)
        {
            //Debug.WriteLine( "     --> On Material: " 
            //  + node.MaterialId + ": " + node.NodeName );

            // OnMaterial method can be invoked for every 
            // single out-coming mesh even when the material 
            // has not actually changed. Thus it is usually
            // beneficial to store the current material and 
            // only get its attributes when the material 
            // actually changes.

            var id = node.MaterialId;
            if (ElementId.InvalidElementId != id)
            {
                if (_currentDoc == null)
                    return;

                var m = _currentDoc.GetElement(node.MaterialId);
                SetCurrentMaterial(m.UniqueId);
            }
            else
            {
                //string uid = Guid.NewGuid().ToString();

                // Generate a GUID based on color, 
                // transparency, etc. to avoid duplicating
                // non-element material definitions.

                var iColor = Util.ColorToInt(node.Color);
                var uid = $"MaterialNode_{iColor}_{Util.RealString(node.Transparency * 100)}";

                if (!_materials.ContainsKey(uid))
                {
                    var m = new Container.Material
                    {
                        uuid = uid,
                        type = "MeshLambertMaterial",
                        color = iColor
                    };

                    m.ambient = m.color;
                    m.emissive = 0;
                    m.shading = 1;
                    m.opacity = 1; // 128 - material.Transparency;
                    m.opacity =
                        1.0 - node
                            .Transparency; // Revit MaterialNode has double Transparency in ?range?, three.js expects opacity in [0.0,1.0]
                    m.transparent = 0.0 < node.Transparency;
                    m.wireframe = false;

                    _materials.Add(uid, m);
                }
                SetCurrentMaterial(uid);
            }
        }

        public bool IsCanceled()
        {
            // This method is invoked many 
            // times during the export process.

            return false;
        }

        // Removed in Revit 2017:
        //public void OnDaylightPortal( DaylightPortalNode node )
        //{
        //  Debug.WriteLine( "OnDaylightPortal: " + node.NodeName );
        //  Asset asset = node.GetAsset();
        //  Debug.WriteLine( "OnDaylightPortal: Asset:"
        //    + ( ( asset != null ) ? asset.Name : "Null" ) );
        //}

        public void OnRPC(RPCNode node)
        {
            Debug.WriteLine("OnRPC: " + node.NodeName);
            //Asset asset = node.GetAsset();
            //Debug.WriteLine("OnRPC: Asset:"
            //  + ((asset != null) ? asset.Name : "Null"));
        }

        public RenderNodeAction OnViewBegin(ViewNode node)
        {
            Debug.WriteLine("OnViewBegin: "
                            + node.NodeName + "(" + node.ViewId.IntegerValue
                            + "): LOD: " + node.LevelOfDetail);

            return RenderNodeAction.Proceed;
        }

        public void OnViewEnd(ElementId elementId)
        {
            Debug.WriteLine("OnViewEnd: Id: " + elementId.IntegerValue);
            // Note: This method is invoked even for a view that was skipped.
        }

        public RenderNodeAction OnElementBegin(ElementId elementId)
        {
            var e = _currentDoc.GetElement(elementId);

            // note: because of links and that the linked models might have had the same template, we need to 
            // make this further unique...
            var uid = e.UniqueId + "_" + _currentDoc.Title;

            Debug.WriteLine($"OnElementBegin: id {elementId.IntegerValue} category {e.Category.Name} name {e.Name}");

            if (_objects.ContainsKey(uid))
            {
                Debug.WriteLine("\r\n*** Duplicate element!\r\n");
                return RenderNodeAction.Skip;
            }

            if (null == e.Category)
            {
                Debug.WriteLine("\r\n*** Non-category element!\r\n");
                return RenderNodeAction.Skip;
            }

            _elementStack.Push(elementId);

            var idsMaterialGeometry = e.GetMaterialIds(false);
            var n = idsMaterialGeometry.Count;

            if (1 < n)
            {
                Debug.Print("{0} has {1} materials: {2}",
                    Util.ElementDescription(e), n,
                    string.Join(", ", idsMaterialGeometry.Select(id => _currentDoc.GetElement(id).Name)));
            }

            // We handle a current element, which may either
            // be identical to the current object and have
            // one single current geometry or have 
            // multiple current child objects each with a 
            // separate current geometry.

            _currentElement = new Container.Object();

            _currentElement.name = Util.ElementDescription(e);
            _currentElement.material = _currentMaterialUid;
            _currentElement.matrix = new double[] {1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1};
            _currentElement.type = "RevitElement";
            _currentElement.uuid = uid;

            _currentObject = new Dictionary<string, Container.Object>();
            _currentGeometry = new Dictionary<string, Container.Geometry>();
            _vertices = new Dictionary<string, VertexLookupInt>();

            if (null != e.Category
                && null != e.Category.Material)
            {
                SetCurrentMaterial(e.Category.Material.UniqueId);
            }

            return RenderNodeAction.Proceed;
        }

        public void OnElementEnd(ElementId id)
        {
            // Note: this method is invoked even for 
            // elements that were skipped.

            var e = _currentDoc.GetElement(id);
            var uid = e.UniqueId;

            Debug.WriteLine($"OnElementEnd: id {id.IntegerValue} category {e.Category.Name} name {e.Name}");

            if (_elementStack.Contains(id) == false) return; // it was skipped?

            if (_objects.ContainsKey(uid))
            {
                Debug.WriteLine("\r\n*** Duplicate element!\r\n");
                return;
            }

            if (null == e.Category)
            {
                Debug.WriteLine("\r\n*** Non-category element!\r\n");
                return;
            }

            var materials = _vertices.Keys.ToList();
            var n = materials.Count;

            _currentElement.children = new List<Container.Object>(n);

            foreach (string material in materials)
            {
                var obj = _currentObject[material];
                var geo = _currentGeometry[material];

                foreach (var p in _vertices[material])
                {
                    geo.data.vertices.Add(_scale_vertex * p.Key.X);
                    geo.data.vertices.Add(_scale_vertex * p.Key.Y);
                    geo.data.vertices.Add(_scale_vertex * p.Key.Z);
                }
                obj.geometry = geo.uuid;

                //QUESTION: Should we attempt to further ensure uniqueness? or should we just update the geometry that is there?
                //old: _geometries.Add(geo.uuid, geo);
                _geometries[geo.uuid] = geo;
                _currentElement.children.Add(obj);
            }

            // var d = Util.GetElementProperties(e, true);
            // var d = Util.GetElementFilteredProperties(e, true); 
            var d = Util.GetElementProperties(e, true);
            var layerName = e.Category.Name;

            //add a property for layer
            d.Add("layer", layerName);



            if (!_viewsAndLayersDict.ContainsKey("layers")) _viewsAndLayersDict.Add("layers", layerName);
            else
            {
                if (!_layerList.Contains(layerName))
                {
                    _viewsAndLayersDict["layers"] += "," + layerName;
                }
            }

            if (!_layerList.Contains(layerName)) _layerList.Add(layerName);

            _currentElement.userData = d;

            //also add guid to user data dictionary
            _currentElement.userData.Add("revit_id", uid);

            _objects[_currentElement.uuid] = _currentElement;

            _elementStack.Pop();
        }

        public RenderNodeAction OnFaceBegin(FaceNode node)
        {
            // This method is invoked only if the 
            // custom exporter was set to include faces.

            // Debug.Assert(false, "we set exporter.IncludeFaces false");
            Debug.WriteLine("  OnFaceBegin: " + node.NodeName);
            return RenderNodeAction.Proceed;
        }

        public void OnFaceEnd(FaceNode node)
        {
            // This method is invoked only if the 
            // custom exporter was set to include faces.

            // Debug.Assert(false, "we set exporter.IncludeFaces false");
            Debug.WriteLine("  OnFaceEnd: " + node.NodeName);
            // Note: This method is invoked even for faces that were skipped.
        }

        public RenderNodeAction OnInstanceBegin(InstanceNode node)
        {
            Debug.WriteLine("  OnInstanceBegin: " + node.NodeName
                            + " symbol: " + node.GetSymbolId().IntegerValue);
            // This method marks the start of processing a family instance
            _transformationStack.Push(CurrentTransform.Multiply(node.GetTransform()));

            // We can either skip this instance or proceed with rendering it.
            return RenderNodeAction.Proceed;
        }

        public void OnInstanceEnd(InstanceNode node)
        {
            Debug.WriteLine("  OnInstanceEnd: " + node.NodeName);
            // Note: This method is invoked even for instances that were skipped.
            _transformationStack.Pop();
        }

        public RenderNodeAction OnLinkBegin(LinkNode node)
        {
            Debug.WriteLine("  OnLinkBegin: " + node.NodeName + " Document: " + node.GetDocument().Title + ": Id: " +
                            node.GetSymbolId().IntegerValue);
            _currentDoc = node.GetDocument();
            _transformationStack.Push(CurrentTransform.Multiply(node.GetTransform()));
            return RenderNodeAction.Proceed;
        }

        public void OnLinkEnd(LinkNode node)
        {
            Debug.WriteLine("  OnLinkEnd: " + node.NodeName);
            // reset for the original document
            _currentDoc = _doc;

            // Note: This method is invoked even for instances that were skipped.
            _transformationStack.Pop();
        }

        public void OnLight(LightNode node)
        {
            Debug.WriteLine("OnLight: " + node.NodeName);
            //Asset asset = node.GetAsset();
            //Debug.WriteLine("OnLight: Asset:" + ((asset != null) ? asset.Name : "Null"));
        }
    }
}
