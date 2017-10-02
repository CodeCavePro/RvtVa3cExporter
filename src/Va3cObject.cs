//The MIT License (MIT)

//Those portions created by va3c authors are provided with the following copyright:

//Copyright (c) 2014 va3c

//Those portions created by Thornton Tomasetti employees are provided with the following copyright:

//Copyright (c) 2015 Thornton Tomasetti

//Those portions created by CodeCave employees are provided with the following copyright:

//Copyright (c) 2017 CodeCave

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

using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

#endregion // Namespaces

namespace RvtVa3c
{
    /// <summary>
    /// three.js object class, successor of Va3cScene.
    /// The structure and properties defined here were
    /// reverse engineered from JSON files exported 
    /// by the three.js and Va3c editors.
    /// </summary>
    [DataContract]
    public class Va3cContainer
    {
        /// <summary>
        /// Based on MeshPhongMaterial obtained by 
        /// exporting a cube from the three.js editor.
        /// </summary>
        public class Va3cMaterial
        {
            [DataMember(Name = "uuid")]
            [JsonProperty(PropertyName = "uuid")]
            public string UUID { get; set; }

            [DataMember(Name = "name")]
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [DataMember(Name = "type")]
            [JsonProperty(PropertyName = "type")]
            public string Type { get; set; } // MeshPhongMaterial

            [DataMember(Name = "color")]
            [JsonProperty(PropertyName = "color")]
            public int Color { get; set; } // 16777215

            [DataMember(Name = "ambient")]
            [JsonProperty(PropertyName = "ambient")]
            public int Ambient { get; set; } //16777215

            [DataMember(Name = "emissive")]
            [JsonProperty(PropertyName = "emissive")]
            public int Emissive { get; set; } // 1

            [DataMember(Name = "opacity")]
            [JsonProperty(PropertyName = "opacity")]
            public double Opacity { get; set; } // 1

            [DataMember(Name = "transparent")]
            [JsonProperty(PropertyName = "transparent")]
            public bool Transparent { get; set; } // false

            [DataMember(Name = "wireframe")]
            [JsonProperty(PropertyName = "wireframe")]
            public bool Wireframe { get; set; } // false

            [DataMember(Name = "shading")]
            [JsonProperty(PropertyName = "shading")]
            public int Shading { get; set; } // 1
        }

        [DataContract]
        public class Va3cGeometryData
        {
            [DataMember(Name = "vertices")]
            [JsonProperty(PropertyName = "vertices")]
            public List<double> Vertices { get; set; } // millimeters

            // "morphTargets": []
            [DataMember(Name = "normals")]
            [JsonProperty(PropertyName = "normals")]
            public List<double> Normals { get; set; }

            // "colors": []
            [DataMember(Name = "uvs")]
            [JsonProperty(PropertyName = "uvs")]
            public List<double> UVs { get; set; }

            [DataMember(Name = "faces")]
            [JsonProperty(PropertyName = "faces")]
            public List<int> Faces { get; set; } // indices into Vertices + Materials

            [DataMember(Name = "scale")]
            [JsonProperty(PropertyName = "scale")]
            public double Scale { get; set; }

            [DataMember(Name = "visible")]
            [JsonProperty(PropertyName = "visible")]
            public bool Visible { get; set; }

            [DataMember(Name = "castShadow")]
            [JsonProperty(PropertyName = "castShadow")]
            public bool CastShadow { get; set; }

            [DataMember(Name = "receiveShadow")]
            [JsonProperty(PropertyName = "receiveShadow")]
            public bool ReceiveShadow { get; set; }

            [DataMember(Name = "doubleSided")]
            [JsonProperty(PropertyName = "doubleSided")]
            public bool DoubleSided { get; set; }
        }

        [DataContract]
        public class Va3cGeometry
        {
            [DataMember(Name = "uuid")]
            [JsonProperty(PropertyName = "uuid")]
            public string UUID { get; set; }

            [DataMember(Name = "type")]
            [JsonProperty(PropertyName = "type")]
            public string Type { get; set; } // "Geometry"

            [DataMember(Name = "data")]
            [JsonProperty(PropertyName = "data")]
            public Va3cGeometryData Data { get; set; }

            //[DataMember] public double scale { get; set; }
            [DataMember(Name = "materials")]
            [JsonProperty(PropertyName = "materials")]
            public List<Va3cMaterial> Materials { get; set; }
        }

        [DataContract]
        public class Va3cObject
        {
            [DataMember(Name = "uuid")]
            [JsonProperty(PropertyName = "uuid")]
            public string UUID { get; set; }

            [DataMember(Name = "name")]
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; } // BIM <document name>

            [DataMember(Name = "type")]
            [JsonProperty(PropertyName = "type")]
            public string Type { get; set; } // Object3D

            [DataMember(Name = "matrix")]
            [JsonProperty(PropertyName = "matrix")]
            public double[] Matrix { get; set; } // [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1]

            [DataMember(Name = "children")]
            [JsonProperty(PropertyName = "children")]
            public List<Va3cObject> Children { get; set; }

            // The following are only on the children:
            [DataMember(Name = "geometry")]
            [JsonProperty(PropertyName = "geometry")]
            public string Geometry { get; set; }

            [DataMember(Name = "material")]
            [JsonProperty(PropertyName = "material")]
            public string Material { get; set; }

            [DataMember(Name = "userData")]
            [JsonProperty(PropertyName = "userData")]
            public Dictionary<string, string> UserData { get; set; }
        }

        // https://github.com/mrdoob/three.js/wiki/JSON-Model-format-3

        // for the faces, we will use
        // triangle with material
        // 00 00 00 10 = 2
        // 2, [vertex_index, vertex_index, vertex_index], [material_index]     // e.g.:
        //
        //2, 0,1,2, 0

        public class Va3cMetadata
        {
            [DataMember(Name = "type")]
            [JsonProperty(PropertyName = "type")]
            public string Type { get; set; } //  "Object"

            [DataMember(Name = "version")]
            [JsonProperty(PropertyName = "version")]
            public string Version { get; set; } // 4.5.x

            [DataMember(Name = "generator")]
            [JsonProperty(PropertyName = "generator")]
            public string Generator { get; set; } //  "Revit Va3c exporter"
        }

        [DataMember(Name = "metadata")]
        [JsonProperty(PropertyName = "metadata")]
        public Va3cMetadata Metadata { get; set; }

        [DataMember(Name = "object")]
        [JsonProperty(PropertyName = "object")]
        public Va3cObject Object { get; set; }

        [DataMember(Name = "geometries")]
        [JsonProperty(PropertyName = "geometries")]
        public List<Va3cGeometry> Geometries;

        [DataMember(Name = "materials")]
        [JsonProperty(PropertyName = "materials")]
        public List<Va3cMaterial> Materials;
    }
}
