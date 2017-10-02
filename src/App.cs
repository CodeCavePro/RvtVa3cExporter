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

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

#endregion

namespace RvtVa3c
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
            PopulatePanel(application.CreateRibbonPanel("OpenHoReCa"));

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
            return Result.Succeeded;
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
            var pbd = new PushButtonData("Three.js JSON Scene Exporter", "Three.js JSON \r\n Scene Exporter", assemblyPath, "RvtVa3c.Command")
            {
                //add tooltip
                ToolTip = "Export the current project's 3D view as a JSON scene file viewable in Three.js-driven Va3c viewer."
            };

            //add icons
            try
            {
                var icon = new Bitmap(Properties.Resources.RvtVa3c);
                pbd.LargeImage = ImageSourceForBitmap(icon);
            }
            catch (Exception)
            {
                // TODO log the error
            }

            panel.AddItem(pbd);
        }

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        public static ImageSource ImageSourceForBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }

        #endregion Helper methods
    }
}