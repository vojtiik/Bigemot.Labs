//----------------------------------------------------------------------------
//  Copyright (C) 2004-2014 by EMGU Corporation. All rights reserved.       
//----------------------------------------------------------------------------

using System;
using System.Drawing;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using Emgu.CV.Util;

namespace SURFFeature
{
    public partial class CameraCapture : Form
    {
        private Capture _capture = null;
        private bool _captureInProgress;
        public Image<Bgr, byte> ModelImage = new Image<Bgr, byte>("vanoce.jpg");
        private ImageMatcher matcher = new ImageMatcher(500, 0.6, 2);

        public CameraCapture()
        {
            InitializeComponent();
            CvInvoke.UseOpenCL = false;

            matcher.SetModelImage(ModelImage);

            Mat result = new Mat();
            Features2DToolbox.DrawKeypoints(ModelImage, matcher.ModelKeyPoints, result, new Bgr(125, 33, 113), Features2DToolbox.KeypointDrawType.Default);
            ImageViewer.Show(result, "model");

            try
            {
                _capture = new Capture();
                _capture.ImageGrabbed += ProcessFrame;
                _capture.Start();
            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }
        }

        private void ProcessFrame(object sender, EventArgs arg)
        {
            var frame = new Mat();
            _capture.Retrieve(frame, 0);

            using (var observedImage = frame.ToImage<Bgr, byte>())
            {
                var homo = matcher.DetermineHomography(observedImage);
                if (homo != null)
                {
                    var resultMatrix = GetResultMatrix(homo, ModelImage, matcher.ModelKeyPoints, observedImage,
                        matcher.ObservedKeyPoints, matcher.Matches, matcher.Mask);
                    captureImageBox.Image = resultMatrix.ToImage<Bgr, byte>();
                }
                else
                {
                    captureImageBox.Image = frame;
                }
            }
        }

        public Mat GetResultMatrix(HomographyMatrix homo, Image<Bgr, Byte> modelImage, VectorOfKeyPoint modelKeyPoints, Image<Bgr, Byte> observedImage, VectorOfKeyPoint observedKeyPoints, VectorOfVectorOfDMatch matches, Matrix<byte> mask)
        {
            Mat result = new Mat();
            Features2DToolbox.DrawKeypoints(observedImage, observedKeyPoints, result, new Bgr(125, 33, 113), Features2DToolbox.KeypointDrawType.Default);

            //draw a rectangle along the projected model
            Rectangle rect = modelImage.ROI;
            PointF[] pts =
                    {
                        new PointF(rect.Left, rect.Bottom),
                        new PointF(rect.Right, rect.Bottom),
                        new PointF(rect.Right, rect.Top),
                        new PointF(rect.Left, rect.Top)
                    };
            homo.ProjectPoints(pts);

            Point[] points = Array.ConvertAll<PointF, Point>(pts, Point.Round);
            using (var vp = new VectorOfPoint(points))
            {
                CvInvoke.Polylines(result, vp, true, new MCvScalar(255, 0, 0, 255), 5);
            }

            return result;
        }
    }
}
