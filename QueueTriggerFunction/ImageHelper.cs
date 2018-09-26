using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace BeerFunction
{
    /// <summary>
    /// Helper class for adding text on an image.
    /// </summary>
    public class ImageHelper
    {
        /// <summary>
        /// Adds text on an image.
        /// </summary>
        /// <param name="memoryStream">The memory stream.</param>
        /// <param name="beerAdvice">The beer advice.</param>
        /// <param name="temperature">The temperature.</param>
        /// <param name="weatherDescription">The weather description.</param>
        /// <param name="windspeed">The wind speed.</param>
        /// <returns>A memory stream.</returns>
        public MemoryStream AddTextToImage(MemoryStream memoryStream, string beerAdvice, string temperature, string weatherDescription, string windspeed)
        {
            Bitmap bitmap = (Bitmap)Image.FromStream(memoryStream);

            Graphics graphics = Graphics.FromImage(bitmap);

            Font font = new Font("Verdana", 14, FontStyle.Bold);

            graphics.DrawString("Advies: " + beerAdvice, font, Brushes.Red, new PointF(10f, 10f));
            graphics.DrawString("Temperatuur: " + temperature + " C", font, Brushes.Red, new PointF(10f, 40f));
            graphics.DrawString("Description: " + weatherDescription, font, Brushes.Red, new PointF(10f, 70f));
            graphics.DrawString("Windsnelheid: " + windspeed + " m/s", font, Brushes.Red, new PointF(10f, 100f));

            MemoryStream outMemoryStream = new MemoryStream();
            bitmap.Save(outMemoryStream, ImageFormat.Png);
            outMemoryStream.Position = 0;

            return outMemoryStream;
        }
    }
}
