using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PresetManager;
using PresetManager.Attributes;

namespace RawBayer2DNG
{
    class R2DSettings : AppSettings
    {

        // Metadata for dng
        [Category("Metadata")]
        [Control("txtMake")]
        public string metaMake = "";
        [Category("Metadata")]
        [Control("txtModel")]
        public string metaModel = "";
        [Category("Metadata")]
        [Control("txtSoftware")]
        public string metaSoftware = "";
        [Category("Metadata")]
        [Control("txtUniqueCameraModel")]
        public string metaUniqueCameraModel = "";

        // Raw bayer reading settings
        public enum InputFormat
        {
            [Control("formatRadio_rg16")]
            RAW16BIT,
            [Control("formatRadio_rg12p")]
            RAW12P
        }
        [Category("RawInterpret")]
        public InputFormat inputFormat = InputFormat.RAW16BIT;

    }
}
