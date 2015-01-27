//----------------------------------------------------------------------------
//  Copyright (C) 2004-2014 by EMGU Corporation. All rights reserved.       
//----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.XFeatures2D;
using log4net;

namespace SURFFeature
{
    public class ImageMatcher
    {
        private readonly double _hessianThresh;
        private readonly double _uniquenessThreshold;
        private readonly int _k;
        private SURFDetector _surfCpu;

        private int ProcessedImages = 0;

        public ImageMatcher(double hessianThresh, double uniquenessThreshold, int k)
        {
            _hessianThresh = hessianThresh;
            _uniquenessThreshold = uniquenessThreshold;
            _k = k;
        }

        public VectorOfKeyPoint ModelKeyPoints { get; set; }
        public VectorOfKeyPoint ObservedKeyPoints { get; set; }
        public VectorOfVectorOfDMatch Matches { get; set; }
        public Matrix<byte> Mask { get; set; }

        private UMat _modelDescriptors;

        public void SetModelImage(Image<Bgr, Byte> modelImage)
        {
            using (UMat uModelImage = modelImage.Mat.ToUMat(AccessType.Read))
            {
                ModelKeyPoints = new VectorOfKeyPoint();
                _modelDescriptors = new UMat();
                _surfCpu = new SURFDetector(_hessianThresh); //,10,8);
                _surfCpu.DetectAndCompute(uModelImage, null, ModelKeyPoints, _modelDescriptors, false);
            }
        }

        public HomographyMatrix DetermineHomography(Image<Bgr, byte> observedImage)
        {
            ProcessedImages++;

            ObservedKeyPoints = new VectorOfKeyPoint();
            Matches = new VectorOfVectorOfDMatch();

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            
            using (var uObservedImage = observedImage.Mat.ToUMat(AccessType.Read))
            {
                var observedDescriptors = new UMat();
                
                var stopWatch2 = new Stopwatch();
                stopWatch2.Start();
                
                _surfCpu.DetectAndCompute(uObservedImage, null, ObservedKeyPoints, observedDescriptors, false);

                StopWatchAndLogElapsed(stopWatch2, "DetectAndCompute");

                var matcher = new BFMatcher(DistanceType.L2);
                matcher.Add(_modelDescriptors);

                var stopWatch3 = new Stopwatch();
                stopWatch3.Start();
                
                matcher.KnnMatch(observedDescriptors, Matches, _k, null);
                
                StopWatchAndLogElapsed(stopWatch3, "KnnMatch");
             
                Mask = new Matrix<byte>(Matches.Size, 1);
                Mask.SetValue(255);
                Features2DToolbox.VoteForUniqueness(Matches, _uniquenessThreshold, Mask);

                var nonZeroCount = CvInvoke.CountNonZero(Mask);
                if (nonZeroCount < 4)
                {
                    StopWatchAndLogElapsed(stopWatch, "DetermineHomography - fail no match");
                    return null;
                }
                
                nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(ModelKeyPoints, ObservedKeyPoints, Matches, Mask, 1.5, 20);
                if (nonZeroCount >= 4)
                {
                    StopWatchAndLogElapsed(stopWatch, "DetermineHomography - success");
                    return Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(ModelKeyPoints, ObservedKeyPoints, Matches, Mask, 2);
                }
            }

            StopWatchAndLogElapsed(stopWatch, "DetermineHomography - fail no match");
            return null;
        }

        private void StopWatchAndLogElapsed(Stopwatch stopWatch,string stopWatchName )
        {
            stopWatch.Stop();
            LogManager.GetLogger("default").InfoFormat("Image {2} : {0} elapsed in: {1} ", stopWatchName, stopWatch.ElapsedMilliseconds, ProcessedImages);
        }
    }
}
