// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FaceTrackingViewer.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace FaceTrackingBasics
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit.FaceTracking;

    using Point = System.Windows.Point;
    using System.Globalization;

    /// <summary>
    /// Class that uses the Face Tracking SDK to display a face mask for
    /// tracked skeletons
    /// </summary>
    public partial class FaceTrackingViewer : UserControl, IDisposable
    {
        public static readonly DependencyProperty KinectProperty = DependencyProperty.Register(
            "Kinect", 
            typeof(KinectSensor), 
            typeof(FaceTrackingViewer), 
            new PropertyMetadata(
                null, (o, args) => ((FaceTrackingViewer)o).OnSensorChanged((KinectSensor)args.OldValue, (KinectSensor)args.NewValue)));

        private const uint MaxMissedFrames = 100;

        private readonly Dictionary<int, SkeletonFaceTracker> trackedSkeletons = new Dictionary<int, SkeletonFaceTracker>();

        private byte[] colorImage;

        private ColorImageFormat colorImageFormat = ColorImageFormat.Undefined;

        private short[] depthImage;

        private DepthImageFormat depthImageFormat = DepthImageFormat.Undefined;

        private bool disposed;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        private DrawingContext drawingContext;

        private Skeleton[] skeletonData;

        private static double faceWidth;

        private static double faceLenght;

        public static double jawLowerer;
        public static double eyeBrow;
        public static double lipCornerDepressor;
        public static double upperLipRaiser;
        public static double lipStrecher;
        public static double outerBrowraiser;

        public static bool isLeftEyeOn = true;
        public static bool isRightEyeOn = true;
        public static bool isMouthOn = true;
        public static bool isTopHeadOn = false;
        public static bool isChinOn = false;
        public static bool isFaceModelOn = false;
        public static bool IsLookingToSensor = false;

        public static Point leftEyeCenter;
        public static Point rightEyeCenter;
        public static Point chinCenter;


        public static List<Vector3DF> leftEyePoints3D = new List<Vector3DF>();
        public static List<Vector3DF> rightEyePoints3D = new List<Vector3DF>();
        public static List<Vector3DF> mountPoints3D = new List<Vector3DF>();

        public static List<Point> leftEyePoints2D = new List<Point>();
        public static List<Point> rightEyePoints2D = new List<Point>();
        public static List<Point> mountPoints2D = new List<Point>();

        public FaceTrackingViewer()
        {
            this.InitializeComponent();
        }

        public double GetFaceWidth(){
         return faceWidth;
        }

        public double GetFaceLenght()
        {
            return faceLenght;
        }

        ~FaceTrackingViewer()
        {
            this.Dispose(false);
        }

        public KinectSensor Kinect
        {
            get
            {
                return (KinectSensor)this.GetValue(KinectProperty);
            }

            set
            {
                this.SetValue(KinectProperty, value);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                this.ResetFaceTracking();

                this.disposed = true;
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            this.drawingContext = drawingContext;
            foreach (SkeletonFaceTracker faceInformation in this.trackedSkeletons.Values)
            {
                faceInformation.DrawFaceModel(drawingContext);
            }
        }

        private void OnAllFramesReady(object sender, AllFramesReadyEventArgs allFramesReadyEventArgs)
        {
            ColorImageFrame colorImageFrame = null;
            DepthImageFrame depthImageFrame = null;
            SkeletonFrame skeletonFrame = null;

            try
            {
                colorImageFrame = allFramesReadyEventArgs.OpenColorImageFrame();
                depthImageFrame = allFramesReadyEventArgs.OpenDepthImageFrame();
                skeletonFrame = allFramesReadyEventArgs.OpenSkeletonFrame();

                if (colorImageFrame == null || depthImageFrame == null || skeletonFrame == null)
                {
                    return;
                }

                // Check for image format changes.  The FaceTracker doesn't
                // deal with that so we need to reset.
                if (this.depthImageFormat != depthImageFrame.Format)
                {
                    this.ResetFaceTracking();
                    this.depthImage = null;
                    this.depthImageFormat = depthImageFrame.Format;
                }

                if (this.colorImageFormat != colorImageFrame.Format)
                {
                    this.ResetFaceTracking();
                    this.colorImage = null;
                    this.colorImageFormat = colorImageFrame.Format;
                }

                // Create any buffers to store copies of the data we work with
                if (this.depthImage == null)
                {
                    this.depthImage = new short[depthImageFrame.PixelDataLength];
                }

                if (this.colorImage == null)
                {
                    this.colorImage = new byte[colorImageFrame.PixelDataLength];
                }
                
                // Get the skeleton information
                if (this.skeletonData == null || this.skeletonData.Length != skeletonFrame.SkeletonArrayLength)
                {
                    this.skeletonData = new Skeleton[skeletonFrame.SkeletonArrayLength];
                }

                colorImageFrame.CopyPixelDataTo(this.colorImage);
                depthImageFrame.CopyPixelDataTo(this.depthImage);
                skeletonFrame.CopySkeletonDataTo(this.skeletonData);

                // Update the list of trackers and the trackers with the current frame information
                foreach (Skeleton skeleton in this.skeletonData)
                {
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked
                        || skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
                    {
                        // We want keep a record of any skeleton, tracked or untracked.
                        if (!this.trackedSkeletons.ContainsKey(skeleton.TrackingId))
                        {
                            SkeletonFaceTracker skeletonFace = new SkeletonFaceTracker();
                            skeletonFace.Kinect = this.Kinect;
                            this.trackedSkeletons.Add(skeleton.TrackingId, skeletonFace);
                        }

                        // Give each tracker the upated frame.
                        SkeletonFaceTracker skeletonFaceTracker;

                        if (this.trackedSkeletons.TryGetValue(skeleton.TrackingId, out skeletonFaceTracker))
                        {
                            skeletonFaceTracker.OnFrameReady(this.Kinect, colorImageFormat, colorImage, depthImageFormat, depthImage, skeleton);
                            skeletonFaceTracker.LastTrackedFrame = skeletonFrame.FrameNumber;
                        }
                    }
                }

                this.RemoveOldTrackers(skeletonFrame.FrameNumber);

                this.InvalidateVisual();
            }
            finally
            {
                if (colorImageFrame != null)
                {
                    colorImageFrame.Dispose();
                }

                if (depthImageFrame != null)
                {
                    depthImageFrame.Dispose();
                }

                if (skeletonFrame != null)
                {
                    skeletonFrame.Dispose();
                }
            }
        }

        private void OnSensorChanged(KinectSensor oldSensor, KinectSensor newSensor)
        {
            if (oldSensor != null)
            {
                oldSensor.AllFramesReady -= this.OnAllFramesReady;
                this.ResetFaceTracking();
            }

            if (newSensor != null)
            {
                newSensor.AllFramesReady += this.OnAllFramesReady;
                            }
        }



        /// <summary>
        /// Clear out any trackers for skeletons we haven't heard from for a while
        /// </summary>
        private void RemoveOldTrackers(int currentFrameNumber)
        {
            var trackersToRemove = new List<int>();

            foreach (var tracker in this.trackedSkeletons)
            {
                uint missedFrames = (uint)currentFrameNumber - (uint)tracker.Value.LastTrackedFrame;
                if (missedFrames > MaxMissedFrames)
                {
                    // There have been too many frames since we last saw this skeleton
                    trackersToRemove.Add(tracker.Key);
                }
            }

            foreach (int trackingId in trackersToRemove)
            {
                this.RemoveTracker(trackingId);
            }
        }

        private void RemoveTracker(int trackingId)
        {
            this.trackedSkeletons[trackingId].Dispose();
            this.trackedSkeletons.Remove(trackingId);
        }

        private void ResetFaceTracking()
        {
            foreach (int trackingId in new List<int>(this.trackedSkeletons.Keys))
            {
                this.RemoveTracker(trackingId);
            }
        }

        private class SkeletonFaceTracker : IDisposable
        {
            private static FaceTriangle[] faceTriangles;

            private EnumIndexableCollection<FeaturePoint, PointF> facePoints;

            private EnumIndexableCollection<FeaturePoint, Vector3DF> faceShapePoints;

            private FaceTracker faceTracker;

            private bool lastFaceTrackSucceeded;

            private SkeletonTrackingState skeletonTrackingState;

            public int LastTrackedFrame { get; set; }

            public KinectSensor Kinect { get; set; }

            public void Dispose()
            {
                if (this.faceTracker != null)
                {
                    this.faceTracker.Dispose();
                    this.faceTracker = null;
                }
            }

            public void DrawFaceModel(DrawingContext drawingContext)
            {
                if (!this.lastFaceTrackSucceeded || this.skeletonTrackingState != SkeletonTrackingState.Tracked)
                {
                    return;
                }

                var faceModelPts = new List<Point>();
                var faceModel = new List<FaceModelTriangle>();

                for (int i = 0; i < this.facePoints.Count; i++)
                {
                    faceModelPts.Add(new Point(this.facePoints[i].X + 0.5f, this.facePoints[i].Y + 0.5f));
                }

                foreach (var t in faceTriangles)
                {
                    var triangle = new FaceModelTriangle();
                    triangle.P1 = faceModelPts[t.First];
                    triangle.P2 = faceModelPts[t.Second];
                    triangle.P3 = faceModelPts[t.Third];
                    faceModel.Add(triangle);
                }

                var faceModelGroup = new GeometryGroup();

                faceWidth = faceModel[189].P1.X - faceModel[199].P1.X;
                faceLenght = faceModel[53].P1.Y - faceModel[0].P2.Y;

                for (int i = 0; i < faceModel.Count; i++)
                {
                    var faceTriangle = new GeometryGroup();
                    //faceTriangle.Children.Add(new Point(faceModel[i].P1.X, faceModel[i].P1.Y));
                    faceTriangle.Children.Add(new LineGeometry(faceModel[i].P1, faceModel[i].P2));
                    faceTriangle.Children.Add(new LineGeometry(faceModel[i].P2, faceModel[i].P3));
                    faceTriangle.Children.Add(new LineGeometry(faceModel[i].P3, faceModel[i].P1));

                    faceModelGroup.Children.Add(faceTriangle);
                }

                 SkeletonPoint po = new SkeletonPoint();
                po.X = faceShapePoints[FeaturePoint.InnerBottomRightPupil].X;
                po.Y = faceShapePoints[FeaturePoint.InnerBottomRightPupil].Y;
                po.Z = faceShapePoints[FeaturePoint.InnerBottomRightPupil].Z;
                leftEyeCenter = SkeletonPointToScreen(po);

                SkeletonPoint po1 = new SkeletonPoint();
                po1.X = faceShapePoints[FeaturePoint.InnerBottomLeftPupil].X;
                po1.Y = faceShapePoints[FeaturePoint.InnerBottomLeftPupil].Y;
                po1.Z = faceShapePoints[FeaturePoint.InnerBottomLeftPupil].Z;

                rightEyeCenter = SkeletonPointToScreen(po1);
                chinCenter = new Point(faceShapePoints[FeaturePoint.AboveChin].X, faceShapePoints[FeaturePoint.AboveChin].Y);
                var leftEyeZ = faceShapePoints[FeaturePoint.AboveMidUpperLeftEyelid].Z;
                var rightEyeZ = faceShapePoints[FeaturePoint.AboveMidUpperRightEyelid].Z;
                IsLookingToSensor = Math.Abs(leftEyeZ - rightEyeZ) <= 0.02f;

                if(isChinOn){
                    DrawChinPoints(drawingContext);
                }

                if (isTopHeadOn)
                {
                    DrawTopHeadPoints(drawingContext);
                }

                //mounth region
                if (isMouthOn)
                {
                    GetMouthPoints();
                    foreach (Vector3DF vector in mountPoints3D)
                    {
                        ProjectVector3Dto2D(vector, drawingContext);
                    }
                }

                //left eye region
                if (isRightEyeOn)
                {
                    GetRightEyePoints();
                    foreach (Vector3DF vector in rightEyePoints3D)
                    {
                        ProjectVector3Dto2D(vector, drawingContext);
                    }
                }

                //right eye region
                if (isLeftEyeOn)
                {
                    GetLeftEyePoints();
                    foreach (Vector3DF vector in leftEyePoints3D)
                    {
                        ProjectVector3Dto2D(vector, drawingContext);
                    }
                }

                if(isFaceModelOn)
                drawingContext.DrawGeometry(Brushes.LightYellow, new Pen(Brushes.LightYellow, 1.0), faceModelGroup);

            }


            private void GetLeftEyePoints()
            {
                leftEyePoints3D = new List<Vector3DF>();
                leftEyePoints3D.Add(faceShapePoints[FeaturePoint.LeftOfRightEyebrow]);
                leftEyePoints3D.Add(faceShapePoints[FeaturePoint.MiddleBottomOfRightEyebrow]);
                leftEyePoints3D.Add(faceShapePoints[FeaturePoint.MiddleTopOfRightEyebrow]);
                leftEyePoints3D.Add(faceShapePoints[FeaturePoint.RightOfRightEyebrow]);
                leftEyePoints3D.Add(faceShapePoints[FeaturePoint.BelowThreeFourthRightEyelid]);
                leftEyePoints3D.Add(faceShapePoints[FeaturePoint.AboveThreeFourthRightEyelid]);
                leftEyePoints3D.Add(faceShapePoints[FeaturePoint.AboveOneFourthRightEyelid]);
                leftEyePoints3D.Add(faceShapePoints[FeaturePoint.AboveMidUpperRightEyelid]);
                leftEyePoints3D.Add(faceShapePoints[FeaturePoint.InnerCornerRightEye]);
                leftEyePoints3D.Add(faceShapePoints[FeaturePoint.InnerTopRightPupil]);
                leftEyePoints2D = ProjectTo2D(leftEyePoints3D);
            }

            private void GetRightEyePoints()
            {
                rightEyePoints3D = new List<Vector3DF>();
                rightEyePoints3D.Add(faceShapePoints[FeaturePoint.LeftOfLeftEyebrow]);
                rightEyePoints3D.Add(faceShapePoints[FeaturePoint.MiddleBottomOfLeftEyebrow]);
                rightEyePoints3D.Add(faceShapePoints[FeaturePoint.MiddleTopOfLeftEyebrow]);
                rightEyePoints3D.Add(faceShapePoints[FeaturePoint.RightOfLeftEyebrow]);
                rightEyePoints3D.Add(faceShapePoints[FeaturePoint.BelowThreeFourthLeftEyelid]);
                rightEyePoints3D.Add(faceShapePoints[FeaturePoint.AboveThreeFourthLeftEyelid]);
                rightEyePoints3D.Add(faceShapePoints[FeaturePoint.AboveOneFourthLeftEyelid]);
                rightEyePoints3D.Add(faceShapePoints[FeaturePoint.AboveMidUpperLeftEyelid]);
                rightEyePoints3D.Add(faceShapePoints[FeaturePoint.InnerCornerLeftEye]);
                rightEyePoints3D.Add(faceShapePoints[FeaturePoint.InnerTopLeftPupil]);
                rightEyePoints2D = ProjectTo2D(rightEyePoints3D);
            }

            private void  GetMouthPoints(){
                mountPoints3D = new List<Vector3DF>();
                mountPoints3D.Add(faceShapePoints[FeaturePoint.LeftCornerMouth]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.RightCornerMouth]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.MiddleTopDipUpperLip]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.MiddleTopLowerLip]);
                mountPoints3D.Add(faceShapePoints[111]);
                mountPoints3D.Add(faceShapePoints[112]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.LeftBottomLowerLip]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.LeftBottomUpperLip]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.LeftTopDipUpperLip]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.LeftTopLowerLip]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.LeftTopUpperLip]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.MiddleBottomUpperLip]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.MiddleTopDipUpperLip]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.OutsideLeftCornerMouth]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.OutsideRightCornerMouth]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.RightBottomLowerLip]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.RightBottomUpperLip]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.RightTopDipUpperLip]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.RightTopLowerLip]);
                mountPoints3D.Add(faceShapePoints[FeaturePoint.RightTopUpperLip]);
                mountPoints2D = ProjectTo2D(mountPoints3D);
            }

            private void DrawChinPoints(DrawingContext drawingContext)
            {
                ProjectVector3Dto2D(faceShapePoints[FeaturePoint.AboveChin], drawingContext);
                ProjectVector3Dto2D(faceShapePoints[FeaturePoint.BottomOfLeftCheek], drawingContext);
                ProjectVector3Dto2D(faceShapePoints[FeaturePoint.BottomOfRightCheek], drawingContext);
                ProjectVector3Dto2D(faceShapePoints[FeaturePoint.LeftSideOfCheek], drawingContext);
                ProjectVector3Dto2D(faceShapePoints[FeaturePoint.RightSideOfChin], drawingContext);
                ProjectVector3Dto2D(faceShapePoints[FeaturePoint.BottomOfChin], drawingContext);
            }

            private void DrawTopHeadPoints(DrawingContext drawingContext)
            {
                ProjectVector3Dto2D(faceShapePoints[FeaturePoint.TopSkull], drawingContext);
                ProjectVector3Dto2D(faceShapePoints[FeaturePoint.TopLeftForehead], drawingContext);
                ProjectVector3Dto2D(faceShapePoints[FeaturePoint.TopRightForehead], drawingContext);
            }

            private void ProjectVector3Dto2D(Vector3DF p, DrawingContext drawingContext)
            {
                SkeletonPoint po = new SkeletonPoint();
                po.X = p.X;
                po.Y = p.Y;
                po.Z = p.Z;
                drawingContext.DrawEllipse(Brushes.Yellow, new Pen(Brushes.Yellow, 0.5), SkeletonPointToScreen(po), 0.5, 0.5);
            }

            private List<Point> ProjectTo2D(List<Vector3DF> dVectors){
                List<Point> points2D = new List<Point>();
                foreach (Vector3DF vector in dVectors)
                {
                    SkeletonPoint po = new SkeletonPoint();
                    po.X = vector.X;
                    po.Y = vector.Y;
                    po.Z = vector.Z;
                    DepthImagePoint depthPoint = this.Kinect.CoordinateMapper.MapSkeletonPointToDepthPoint(po, DepthImageFormat.Resolution640x480Fps30);
                    points2D.Add(new Point(depthPoint.X, depthPoint.Y));
                }
                return points2D;
            }

            /// <summary>
            /// Maps a SkeletonPoint to lie within our render space and converts to Point
            /// </summary>
            /// <param name="skelpoint">point to map</param>
            /// <returns>mapped point</returns>
            private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
            {
                // Convert point to depth space.  
                // We are not using depth directly, but we do want the points in our 640x480 output resolution.
                DepthImagePoint depthPoint = this.Kinect.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
                return new Point(depthPoint.X, depthPoint.Y);
            }

            /// <summary>
            /// Updates the face tracking information for this skeleton
            /// </summary>
            internal void OnFrameReady(KinectSensor kinectSensor, ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, short[] depthImage, Skeleton skeletonOfInterest)
            {
                this.skeletonTrackingState = skeletonOfInterest.TrackingState;

                if (this.skeletonTrackingState != SkeletonTrackingState.Tracked)
                {
                    // nothing to do with an untracked skeleton.
                    return;
                }

                if (this.faceTracker == null)
                {
                    try
                    {
                        this.faceTracker = new FaceTracker(kinectSensor);
                    }
                    catch (InvalidOperationException)
                    {
                        // During some shutdown scenarios the FaceTracker
                        // is unable to be instantiated.  Catch that exception
                        // and don't track a face.
                        Debug.WriteLine("AllFramesReady - creating a new FaceTracker threw an InvalidOperationException");
                        this.faceTracker = null;
                    }
                }

                if (this.faceTracker != null)
                {
                    FaceTrackFrame frame = this.faceTracker.Track(
                        colorImageFormat, colorImage, depthImageFormat, depthImage, skeletonOfInterest);

                    this.lastFaceTrackSucceeded = frame.TrackSuccessful;
                    if (this.lastFaceTrackSucceeded)
                    {
                        if (faceTriangles == null)
                        {
                            // only need to get this once.  It doesn't change.
                            faceTriangles = frame.GetTriangles();
                        }

                        this.facePoints = frame.GetProjected3DShape();
                        this.faceShapePoints = frame.Get3DShape();
                        if (frame.TrackSuccessful)
                        {
                            // Retrieve only the Animation Units coeffs.
                            var AUCoeff = frame.GetAnimationUnitCoefficients();

                            jawLowerer = Math.Round(AUCoeff[AnimationUnit.JawLower],3);
                            eyeBrow = Math.Round(AUCoeff[AnimationUnit.BrowRaiser],3);
                            lipCornerDepressor = Math.Round(AUCoeff[AnimationUnit.LipCornerDepressor],3);
                            upperLipRaiser = Math.Round(AUCoeff[AnimationUnit.LipRaiser],3);
                            lipStrecher =  Math.Round(AUCoeff[AnimationUnit.LipStretcher],3);
                            outerBrowraiser = Math.Round(AUCoeff[AnimationUnit.BrowLower],3);

                        }
                    }
                }
            }

            private struct FaceModelTriangle
            {
                public Point P1;
                public Point P2;
                public Point P3;
            }
        }
    }
}