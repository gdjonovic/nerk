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

                    if (i == 189 || i == 199 || i == 0 || i == 53)
                    {
                        // Create the initial formatted text string.
                        FormattedText formattedText = new FormattedText(
                            i.ToString(),
                            CultureInfo.GetCultureInfo("en-us"),
                            FlowDirection.LeftToRight,
                            new Typeface("Verdana"),
                            10,
                            Brushes.White);
                        if (i == 0)
                        {
                            drawingContext.DrawText(formattedText, faceModel[i].P2);
                            drawingContext.DrawEllipse(Brushes.Red, new Pen(Brushes.Red, 1.0), faceModel[i].P2, 2, 2);
                            //Console.WriteLine(i + "- " + faceModel[i].P2.X + "," + faceModel[i].P2.Y + " value");
                        }
                        else
                        {
                            drawingContext.DrawText(formattedText, faceModel[i].P1);
                            drawingContext.DrawEllipse(Brushes.Red, new Pen(Brushes.Red, 1.0), faceModel[i].P1, 2, 2);
                           // Console.WriteLine(i + "- " + faceModel[i].P1.X + "," + faceModel[i].P1.Y + " value");
                        }
                        
                    }
                }

                    ProjectVector3Dto2D(faceShapePoints[FeaturePoint.BottomOfChin], drawingContext);
                    ProjectVector3Dto2D(faceShapePoints[FeaturePoint.TopSkull], drawingContext);
                    
                    var leftEyeZ = faceShapePoints[FeaturePoint.AboveMidUpperLeftEyelid].Z;
                    var rightEyeZ = faceShapePoints[FeaturePoint.AboveMidUpperRightEyelid].Z;
                    var IsLookingToSensor = Math.Abs(leftEyeZ - rightEyeZ) <= 0.02f;
                    if (IsLookingToSensor)
                    {
                        ProjectVector3Dto2D(faceShapePoints[FeaturePoint.InnerTopLeftPupil], drawingContext);
                        ProjectVector3Dto2D(faceShapePoints[FeaturePoint.InnerTopRightPupil], drawingContext);
                    }

                
                //drawingContext.DrawGeometry(Brushes.LightYellow, new Pen(Brushes.LightYellow, 1.0), faceModelGroup);

            }

            private void ProjectVector3Dto2D(Vector3DF p, DrawingContext drawingContext)
            {
                SkeletonPoint po = new SkeletonPoint();
                po.X = p.X;
                po.Y = p.Y;
                po.Z = p.Z;
                drawingContext.DrawEllipse(Brushes.Yellow, new Pen(Brushes.Yellow, 1.0), SkeletonPointToScreen(po), 2, 2);
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
                        Console.WriteLine("points" + faceShapePoints.Count);
                        if (frame.TrackSuccessful)
                        {
                            // Retrieve only the Animation Units coeffs.
                            var AUCoeff = frame.GetAnimationUnitCoefficients();

                            jawLowerer = AUCoeff[AnimationUnit.JawLower];
                            eyeBrow = AUCoeff[AnimationUnit.BrowRaiser];
                            lipCornerDepressor = AUCoeff[AnimationUnit.LipCornerDepressor];
                            upperLipRaiser = AUCoeff[AnimationUnit.LipRaiser];
                            lipStrecher = AUCoeff[AnimationUnit.LipStretcher];
                            outerBrowraiser = AUCoeff[AnimationUnit.BrowLower];

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