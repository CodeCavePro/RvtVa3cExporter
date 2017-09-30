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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

#endregion

namespace Spectacles.RevitExporter
{
    internal class App : IExternalApplication
    {
        /// <summary>
        /// Called when the application is started.
        /// </summary>
        /// <param name="application">A handle to the application being started.</param>
        /// <returns>
        /// Indicates if the external application completes its work successfully.
        /// </returns>
        /// <inheritdoc />
        public Result OnStartup(UIControlledApplication application)
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            PopulatePanel(application.CreateRibbonPanel("Spectacles"));

            return Result.Succeeded;
        }

        /// <summary>
        /// Called when the application is shut down.
        /// </summary>
        /// <param name="application">A handle to the application being shut down.</param>
        /// <returns>
        /// Indicates if the external application completes its work successfully.
        /// </returns>
        /// <inheritdoc />
        public Result OnShutdown(UIControlledApplication application)
        {
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;

            return Result.Succeeded;
        }

        /// <summary>
        /// Custom assembly resolver to find our support
        /// DLL without being forced to place our entire 
        /// application in a sub-folder of the Revit.exe
        /// directory.
        /// </summary>
        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (!args.Name.Contains("Newtonsoft"))
                return null;

            var fileName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
            {
                throw new InvalidDataException("Output folder doesn't exist");
            }

            fileName = Path.Combine(fileName, "Newtonsoft.Json.dll");
            return File.Exists(fileName)
                ? Assembly.LoadFrom(fileName)
                : null;
        }

        #region Helper methods

        /// <summary>
        /// Add buttons for our command
        /// to the ribbon panel.
        /// </summary>
        private static void PopulatePanel(RibbonPanel panel)
        {
            Debug.WriteLine($"Populating the following panel: {panel.Name}");

            var assemblyPath = Assembly.GetExecutingAssembly().Location;

            //new push button for exporter
            var pbd = new PushButtonData("Spectacles Exporter", "Spectacles \r\n Exporter", assemblyPath, "Spectacles.RevitExporter.Command")
            {
                //add tooltip
                ToolTip = "Export the current 3D view as a Spectacles.json file, which can be viewed with the Spectacles Web Viewer."
            };

            //add icons
            try
            {
                pbd.LargeImage = LoadPngImgSource("Spectacles.RevitExporter.Resources.SPECTACLES_file_32px.png");
            }
            catch (Exception)
            {
                // TODO log the error
            }
        }

        /// <summary>
        /// Load an Embedded Resource Image
        /// </summary>
        /// <param name="sourceName">String path to Resource Image</param>
        /// <returns></returns>
        /// <remarks></remarks>
        private static ImageSource LoadPngImgSource(string sourceName)
        {
            try
            {
                // Stream
                using (var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream(sourceName))
                {
                    if (icon == null)
                    {
                        // TODO log the failure
                        return null;
                    }

                    // Decoder
                    var pngDecoder = new PngBitmapDecoder(icon, BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.Default);

                    // Source
                    return pngDecoder.Frames[0].Clone();
                }

            }
            catch (Exception)
            {
                // TODO log the failure
                return null;
            }
        }

        #endregion Helper methods
    }
}