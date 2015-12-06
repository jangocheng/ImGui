﻿using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Cairo;
//using Svg;

namespace IMGUI
{
    public partial class Texture
    {
        static Texture()
        {
            //TODO Use relative path or resource file
            //TODO Destruct these presets
            var bytes = Convert.FromBase64String(DefaultAppIcon_PNG_Base64_string);
            File.WriteAllBytes("D:\\111.png", bytes);
            _presets = new Dictionary<string, Texture>
            {
                {"DefaultAppIcon", new Texture(Convert.FromBase64String(DefaultAppIcon_PNG_Base64_string))}
                //{"Toggle.Off", new Texture( new ImageSurface("W:/VS2013/IMGUI/Resources/Toggle.Off.png") )},//TODO build these resources into IMGUI assembly
                //{"Toggle.On", new Texture( new ImageSurface("W:/VS2013/IMGUI/Resources/Toggle.On.png") )},
            };
        }

        private const string DefaultAppIcon_PNG_Base64_string =
"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";
    }
}