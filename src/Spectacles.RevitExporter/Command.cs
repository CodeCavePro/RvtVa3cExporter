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
using System.Reflection;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

#endregion // Namespaces

namespace Spectacles.RevitExporter
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        /// <summary>
        /// Custom assembly resolver to find our support
        /// DLL without being forced to place our entire 
        /// application in a subfolder of the Revit.exe
        /// directory.
        /// </summary>
        Assembly CurrentDomain_AssemblyResolve(
                object sender,
                ResolveEventArgs args)
        {
            if (args.Name.Contains("Newtonsoft"))
            {
                string filename = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                filename = Path.Combine(filename, "Newtonsoft.Json.dll");

                if (File.Exists(filename))
                {
                    return Assembly.LoadFrom(filename);
                }
            }
            return null;
        }

        /// <summary>
        /// Export a given 3D view to JSON using
        /// our custom exporter context.
        /// </summary>
        public void ExportView3D(View3D view3d, string filename)
        {
            AppDomain.CurrentDomain.AssemblyResolve
                += CurrentDomain_AssemblyResolve;

            Document doc = view3d.Document;

            SpectaclesExportContext context
                = new SpectaclesExportContext(doc, filename);

            CustomExporter exporter = new CustomExporter(
                doc, context);

            // Note: Excluding faces just suppresses the 
            // OnFaceBegin calls, not the actual processing 
            // of face tessellation. Meshes of the faces 
            // will still be received by the context.
            //
            //exporter.IncludeFaces = false; // removed in Revit 2017

            exporter.ShouldStopOnError = false;

            exporter.Export(view3d);
        }

        public static List<string> cameraNames;
        public static List<string> cameraPositions;
        public static List<string> cameraTargets;

        #region UI to Filter Parameters

        public static bool _filterParameters = false;
        public static Dictionary<string, List<string>> _parameterDictionary;
        public static Dictionary<string, List<string>> _toExportDictionary;
        public static bool includeT = false;

        #endregion

        /// <summary>
        /// Store the last user selected output folder
        /// in the current editing session.
        /// </summary>
        static string _output_folder_path = null;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            //expand scope of command arguments
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            if (!SetViewTo3D(uidoc))
            {
                // TODO log the error

                return Result.Failed;
            }

            //make sure we are in a 3D view
            if (doc.ActiveView is View3D)
            {
                //get the name of the active file, and strip off the extension
                string filename = Path.GetFileNameWithoutExtension(doc.PathName);
                if (string.IsNullOrWhiteSpace(filename))
                {
                    filename = doc.Title;
                }

                _output_folder_path = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;
                if (null == _output_folder_path)
                {
                    // Sometimes the command fails if the file is 
                    // detached from central and not saved locally

                    try
                    {
                        _output_folder_path = Path.GetDirectoryName(
                            filename);
                    }
                    catch
                    {
                        TaskDialog.Show("Folder not found",
                            "Please save the file and run the command again.");
                        return Result.Failed;
                    }
                }

                _filterParameters = false;

                // get all the 3D views in the project
                UIDocument uiDoc = uiapp.ActiveUIDocument;
                Document RvtDoc = uiDoc.Document;

                cameraNames = new List<string>();
                cameraPositions = new List<string>();
                cameraTargets = new List<string>();

                IEnumerable<Element> views = null;
                FilteredElementCollector viewCol = new FilteredElementCollector(RvtDoc);

                viewCol.OfClass(typeof(View3D));

                foreach (View3D camera in viewCol)
                {
                    try
                    {
                        if ((camera.IsTemplate == false) && (camera.IsPerspective))
                        {
                            ViewOrientation3D vo = camera.GetOrientation();
                            cameraNames.Add(camera.Name);
                            cameraPositions.Add((-vo.EyePosition.X * 304.8) + "," + (vo.EyePosition.Z * 304.8) + "," +
                                                (vo.EyePosition.Y * 304.8));
                            cameraTargets.Add((-vo.ForwardDirection.X) + "," + (vo.ForwardDirection.Z) + "," +
                                              (vo.ForwardDirection.Y));
                        }
                    }
                    catch
                    {
                    }
                }

                // Save file
                filename = Path.GetFileName(filename) + ".json";

                //export the file
                filename = Path.Combine(_output_folder_path,
                    filename);

                ExportView3D(doc.ActiveView as View3D, filename);

                //return success
                return Result.Succeeded;
            }

            Util.ErrorMsg("You must be in a 3D view to export.");
            return Result.Failed;
        }

        /// <summary>
        /// Sets the view to 3D view.
        /// </summary>
        /// <param name="uidoc">The UI Document.</param>
        /// <returns></returns>
        private bool SetViewTo3D(UIDocument uidoc)
        {
            View3D view = Get3dView(uidoc.Document);

            if (null == view)
            {
                return false;
            }

            uidoc.ActiveView = view;

            using (Transaction trans = new Transaction(uidoc.Document))
            {
                trans.Start("Change to 3D view");

                view.get_Parameter(BuiltInParameter
                    .VIEW_DETAIL_LEVEL).Set(3);

                view.get_Parameter(BuiltInParameter
                    .MODEL_GRAPHICS_STYLE).Set(6);

                trans.Commit();
            }

            return true;
        }

        /// <summary>
        /// Retrieve a suitable 3D view from document.
        /// </summary>
        View3D Get3dView(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(View3D));

            foreach (View3D v in collector)
            {
                Debug.Assert(null != v,
                    "never expected a null view to be returned"
                    + " from filtered element collector");

                // Skip view template here because view 
                // templates are invisible in project 
                // browser

                if (!v.IsTemplate)
                {
                    return v;
                }
            }
            return null;
        }
    }
}
