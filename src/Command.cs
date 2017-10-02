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
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

#endregion // Namespaces

namespace RvtVa3c
{
    [Transaction(TransactionMode.Manual)]
    public class Command : IExternalCommand
    {
        public static List<string> cameraNames;
        public static List<string> cameraPositions;
        public static List<string> cameraTargets;

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            //expand scope of command arguments
            var uiapp = commandData.Application;
            var uidoc = uiapp.ActiveUIDocument;
            var doc = uidoc.Document;

            if (!SetViewTo3D(uidoc))
            {
                // TODO log the error
                return Result.Failed;
            }

            //make sure we are in a 3D view
            if (!(doc.ActiveView is View3D activeView))
            {
                Util.ErrorMsg("You must be in a 3D view to export.");
                return Result.Failed;
            }

            //get the name of the active file, and strip off the extension
            var filename = Path.GetFileNameWithoutExtension(doc.PathName);
            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = doc.Title;
            }

            var outputFolderPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;

            cameraNames = new List<string>();
            cameraPositions = new List<string>();
            cameraTargets = new List<string>();

            var views3D = new FilteredElementCollector(doc).OfClass(typeof(View3D)).OfType<View3D>();
            foreach (var camera in views3D)
            {
                try
                {
                    if (camera.IsTemplate || (!camera.IsPerspective))
                        continue;

                    using (var vo = camera.GetOrientation())
                    {
                        cameraNames.Add(camera.Name);
                        cameraPositions.Add((-vo.EyePosition.X * 304.8) + "," + (vo.EyePosition.Z * 304.8) + "," + (vo.EyePosition.Y * 304.8));
                        cameraTargets.Add((-vo.ForwardDirection.X) + "," + (vo.ForwardDirection.Z) + "," + (vo.ForwardDirection.Y));
                    }
                }
                catch(Exception)
                {
                    // TODO log the exception
                }
            }

            // Save file
            filename = Path.GetFileName(filename) + ".json";

            //export the file
            filename = Path.Combine(outputFolderPath, filename);

            ExportView3D(activeView, filename);

            //return success
            return Result.Succeeded;
        }

        #region Helper methods

        /// <summary>
        /// Sets the view to 3D view.
        /// </summary>
        /// <param name="uidoc">The UI Document.</param>
        /// <returns></returns>
        private static bool SetViewTo3D(UIDocument uidoc)
        {
            var view = Get3DView(uidoc.Document);
            if (null == view)
            {
                return false;
            }

            uidoc.ActiveView = view;

            using (var trans = new Transaction(uidoc.Document))
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
        private static View3D Get3DView(Document doc)
        {
            // Skip view template here because view templates are invisible in project browser
            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D))
                .OfType<View3D>()
                .Where(v => !v.IsTemplate);

            foreach (var view3D in collector)
            {
                Debug.Assert(null != view3D, "never expected a null view to be returned from filtered element collector");
                return view3D;
            }
            return null;
        }

        /// <summary>
        /// Export a given 3D view to JSON using
        /// our custom exporter context.
        /// </summary>
        public void ExportView3D(View3D view3D, string filename)
        {
            var doc = view3D.Document;
            var context = new Va3cExportContext(doc, filename);

            using (var exporter = new CustomExporter(doc, context) { ShouldStopOnError = false })
            {
                exporter.Export(view3D);
            }
        }

        #endregion Helper method
    }
}
