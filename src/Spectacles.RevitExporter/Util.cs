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
using Autodesk.Revit.DB;

#endregion // Namespaces

namespace RvtVa3c
{
    internal class Util
    {
        /// <summary>
        /// Display an error message to the user.
        /// </summary>
        public static void ErrorMsg(string msg)
        {
            Debug.WriteLine(msg);
            // TODO log
        }

        /// <summary>
        /// Return a string for a real number
        /// formatted to two decimal places.
        /// </summary>
        public static string RealString(double a)
        {
            return a.ToString("0.##");
        }

        /// <summary>
        /// Return a string for an XYZ point
        /// or vector with its coordinates
        /// formatted to two decimal places.
        /// </summary>
        public static string PointString(XYZ p)
        {
            return $"({RealString(p.X)},{RealString(p.Y)},{RealString(p.Z)})";
        }

        /// <summary>
        /// Return an integer value for a Revit Color.
        /// </summary>
        public static int ColorToInt(Color color)
        {
            return color.Red << 16
                   | color.Green << 8
                   | color.Blue;
        }

        /// <summary>
        /// Extract a true or false value from the given
        /// string, accepting yes/no, Y/N, true/false, T/F
        /// and 1/0. We are extremely tolerant, i.e., any
        /// value starting with one of the characters y, n,
        /// t or f is also accepted. Return false if no 
        /// valid Boolean value can be extracted.
        /// </summary>
        public static bool GetTrueOrFalse(
            string s,
            out bool val)
        {
            val = false;

            if (s.Equals(Boolean.TrueString,
                StringComparison.OrdinalIgnoreCase))
            {
                val = true;
                return true;
            }
            if (s.Equals(Boolean.FalseString,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (s.Equals("1"))
            {
                val = true;
                return true;
            }
            if (s.Equals("0"))
            {
                return true;
            }
            s = s.ToLower();

            if ('t' == s[0] || 'y' == s[0])
            {
                val = true;
                return true;
            }
            if ('f' == s[0] || 'n' == s[0])
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Return a string describing the given element:
        /// .NET type name,
        /// category name,
        /// family and symbol name for a family instance,
        /// element id and element name.
        /// </summary>
        public static string ElementDescription(
            Element e)
        {
            if (null == e)
            {
                return "<null>";
            }

            // For a wall, the element name equals the
            // wall type name, which is equivalent to the
            // family name ...

            FamilyInstance fi = e as FamilyInstance;

            string typeName = e.GetType().Name;

            string categoryName = (null == e.Category)
                ? string.Empty
                : e.Category.Name + " ";

            string familyName = (null == fi)
                ? string.Empty
                : fi.Symbol.Family.Name + " ";

            string symbolName = (null == fi
                                 || e.Name.Equals(fi.Symbol.Name))
                ? string.Empty
                : fi.Symbol.Name + " ";

            return $"{typeName} {categoryName}{familyName}{symbolName}<{e.Id.IntegerValue} {e.Name}>";
        }

        /// <summary>
        /// Return a dictionary of all the given 
        /// element parameter names and values.
        /// </summary>
        public static Dictionary<string, string> GetElementProperties(Element e, bool includeType)
        {
            var parameters = e.GetOrderedParameters();
            var a = new Dictionary<string, string>(parameters.Count);

            string key;
            string val;

            foreach (var p in parameters)
            {
                key = p.Definition.Name;

                if (a.ContainsKey(key))
                    continue;

                val = StorageType.String == p.StorageType ? p.AsString() : p.AsValueString();

                if (!string.IsNullOrEmpty(val))
                {
                    a.Add(key, val);
                }
            }

            if (includeType)
            {
                var idType = e.GetTypeId();
                if (ElementId.InvalidElementId == idType)
                    return a;

                var doc = e.Document;
                var typ = doc.GetElement(idType);
                parameters = typ.GetOrderedParameters();
                foreach (var p in parameters)
                {
                    key = "Type " + p.Definition.Name;

                    if (a.ContainsKey(key))
                        continue;

                    val = StorageType.String == p.StorageType ? p.AsString() : p.AsValueString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        a.Add(key, val);
                    }
                }
            }
            return a;
        }


        /// <summary>
        /// Return a dictionary of all the given 
        /// element parameter names and values.
        /// </summary>
        public static Dictionary<string, string> GetElementFilteredProperties(Element e, bool includeType, Dictionary<string, List<string>> toExportDictionary)
        {
            var parameters = e.GetOrderedParameters();
            var a = new Dictionary<string, string>(parameters.Count);

            string key;
            string val;
            string cat = e.Category.Name;

            // Make sure that the file has a tab for that category.

            if (!toExportDictionary.ContainsKey(cat))
                return a;

            foreach (var p in parameters)
            {
                key = p.Definition.Name;

                // Check whether the property has been checked.

                if (!toExportDictionary[cat].Contains(key))
                    continue;

                if (a.ContainsKey(key))
                    continue;

                val = StorageType.String == p.StorageType ? p.AsString() : p.AsValueString();
                if (!string.IsNullOrEmpty(val))
                {
                    a.Add(key, val);
                }
            }

            if (!includeType)
                return a;
                
            var idType = e.GetTypeId();
            if (ElementId.InvalidElementId == idType)
                return a;

            var doc = e.Document;
            var typ = doc.GetElement(idType);
            parameters = typ.GetOrderedParameters();

            foreach (var p in parameters)
            {
                key = "Type " + p.Definition.Name;

                // Check whether the property has been checked.

                if (!toExportDictionary[cat].Contains(key)) continue;
                if (a.ContainsKey(key)) continue;
                val = StorageType.String == p.StorageType ? p.AsString() : p.AsValueString();

                if (!string.IsNullOrEmpty(val))
                {
                    a.Add(key, val);
                }
            }
            return a;
        }
    }
}
