//----------------------------------------------------------------------------
//  Copyright (C) 2004-2014 by EMGU Corporation. All rights reserved.       
//----------------------------------------------------------------------------

using System;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.XFeatures2D;

namespace SURFFeature
{
    public class ImageMatcher
    {
        private readonly double _hessianThresh;
        private readonly double _uniquenessThreshold;
        private readonly int _k;
        private SURFDetector _surfCpu;

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
                _surfCpu = new SURFDetector(_hessianThresh);
                _surfCpu.DetectAndCompute(uModelImage, null, ModelKeyPoints, _modelDescriptors, false);
            }
        }

        public HomographyMatrix DetermineHomography(Image<Bgr, byte> observedImage)
        {
            ObservedKeyPoints = new VectorOfKeyPoint();
            Matches = new VectorOfVectorOfDMatch();

            using (var uObservedImage = observedImage.Mat.ToUMat(AccessType.Read))
            {
                var observedDescriptors = new UMat();
                _surfCpu.DetectAndCompute(uObservedImage, null, ObservedKeyPoints, observedDescriptors, false);

                var matcher = new BFMatcher(DistanceType.L2);
                matcher.Add(_modelDescriptors);
                matcher.KnnMatch(observedDescriptors, Matches, _k, null);

                Mask = new Matrix<byte>(Matches.Size, 1);
                Mask.SetValue(255);
                Features2DToolbox.VoteForUniqueness(Matches, _uniquenessThreshold, Mask);

                var nonZeroCount = CvInvoke.CountNonZero(Mask);
                if (nonZeroCount < 25)
                {
                    return null;
                }

                nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(ModelKeyPoints, ObservedKeyPoints, Matches, Mask, 1.5, 20);
                if (nonZeroCount >= 25)
                {
                    return Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(ModelKeyPoints, ObservedKeyPoints, Matches, Mask, 2);
                }
            }

            return null;
        }
    }
}
