using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ClipperLib;
using Grasshopper;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Rhino.Input.Custom;
using static Generative_Urban_prj.Classes.EqualArea;

namespace Generative_Urban_prj.Classes
{
    public class Footprints
    {
        #region global variables
        double MinDepth;
        double MaxDepth;
        double MinLength;
        double MaxLength;
        public int MaxOff;
        public int MinDiff;
        public Polyline PlnPlot;
        public Polyline CourtyardPoly;
        public double FD;
        public double FL;
        public double RangeDepth;
        public double RangeLength;
        List<Curve> Plots;
        public List<NurbsCurve> SplitLines;
        public Vector3d PerpVec;
        public Line OpenLine;

        // for now hardcoded
        double AlleySpace = 5; //half

        public DataTree<Brep> FootprintsOut = new DataTree<Brep>();
        public DataTree<Brep> FootprintsOffset = new DataTree<Brep>();
        public DataTree<Curve> FootprintsOffset2 = new DataTree<Curve>();

        // Faster to initialize only at the start
        Random rnd = new Random();
        #endregion

        //courtyards - open courtyards - superblocks
        public Footprints(double minDepth, double maxDepth, double minLegth, double maxLength, double areaThres, int minDiff, int maxOff, List<Curve> plots, List<Curve> crossCrv) 
        {
            MinDepth = minDepth;
            MaxDepth = maxDepth;
            MinLength = minLegth;
            MaxLength = maxLength;
            Plots = plots;
            if (minDiff < 1) minDiff = 1;
            if (minDiff > 3) minDiff = 3;
            MaxOff = maxOff;
            MinDiff = minDiff;
 
            double rangeDepth = MaxDepth - MinDepth;
            double rangeLength = MaxLength - MinLength;
            RangeDepth = rangeDepth;
            RangeLength = rangeLength;
            List<Brep> bldgOffset = new List<Brep>();
            List<Brep> bldgOffset2 = new List<Brep>();

            // For every plot, create the footprints
            for (int i = 0; i < Plots.Count; i++) 
            {
                // Create path for tree to add buildings of one plot
                GH_Path path = new GH_Path(i);
                // Choose a random Floor Depth (FD) and Floor Length (FL) within range
                double FD = MinDepth + (rnd.NextDouble() * (rangeDepth));
                double FL = MinLength + (rnd.NextDouble() * (rangeLength));

                List<Brep> fp = new List<Brep>();
                List<Curve> lines = new List<Curve>();
                double areaPlot = 0;

                // Check what kind of plot it is
                if (plots[i].IsClosed)
                {
                    areaPlot = AreaMassProperties.Compute(Plots[i]).Area;
                }

                if (!plots[i].IsClosed)
                {
                    fp = OpenCourtyard2(Plots[i], FD, FL);
                    bldgOffset = RandomizeOffset(PlnPlot, CourtyardPoly, fp, FD, FL);
                    FootprintsOut.AddRange(bldgOffset, path);
                    break;
                }

                // Add if statement for row houses/smaller plots
                // and another for plots that are only lines (sparse buildings)
                if (areaPlot < areaThres)
                {
                    fp = Courtyard(Plots[i], FD, FL);
                    bldgOffset = RandomizeOffset(PlnPlot, CourtyardPoly, fp, FD, FL);

                    if (!plots[i].IsClosed)
                    {
                        bldgOffset2 = OpenCourtyard(bldgOffset);
                        bldgOffset = bldgOffset2;
                    }
                    FootprintsOut.AddRange(bldgOffset, path);
                }

                else // else only superplot for now
                {
                    fp = Superplot(Plots[i], crossCrv, FD, FL);
                    FootprintsOut.AddRange(fp, path);
                }

            }
        }
        //towers
        public Footprints(List<Curve> plots)
        {
            Plots = plots;

            double rangeDepth = MaxDepth - MinDepth;
            double rangeLength = MaxLength - MinLength;
            RangeDepth = rangeDepth;
            RangeLength = rangeLength;
            List<Brep> bldgOffset = new List<Brep>();

            // For every plot, create the footprints
            for (int i = 0; i < Plots.Count; i++)
            {
                // Create path for tree to add buildings of one plot
                GH_Path path = new GH_Path(i);

                List<Brep> fp = Tower(Plots[i]);
                FootprintsOut.AddRange(fp, path);
                bldgOffset = fp;
            }
        }
        //row houses
        public Footprints(double minDepth, double maxDepth, double minLegth, double maxLength, int minDiff, int maxOff, List<Curve> plots)
        {
            MinDepth = minDepth;
            MaxDepth = maxDepth;
            MinLength = minLegth;
            MaxLength = maxLength;
            Plots = plots;
            if (minDiff < 1) minDiff = 1;
            if (minDiff > 3) minDiff = 3;
            MaxOff = maxOff;
            MinDiff = minDiff;

            double rangeDepth = MaxDepth - MinDepth;
            double rangeLength = MaxLength - MinLength;
            RangeDepth = rangeDepth;
            RangeLength = rangeLength;
            List<Brep> bldgOffset = new List<Brep>();

            // For every plot, create the footprints
            for (int i = 0; i < Plots.Count; i++)
            {
                // Create path for tree to add buildings of one plot
                GH_Path path = new GH_Path(i);

                List<Brep> fp = Solid(Plots[i]);
                bldgOffset = RandomizeOffsetSolid(PlnPlot, fp, FD);
                FootprintsOut.AddRange(bldgOffset, path);
            }
        }


        public List<Brep> Superplot(Curve plot, List<Curve> cross, double FD, double FL)
        {
            // Output
            List<Brep> fp = new List<Brep>();

            // Create the courtyard curve
            Curve courtyardCrv = Helpers.OffsetPlot(plot, FD, true);
            Brep surface = Brep.CreatePlanarBreps(new List<Curve>() { plot, courtyardCrv }, 0.01)[0];

            #region prepare cut curves and split surface
            List<Curve> cutters = new List<Curve>();
            foreach (var crv in cross)
            {
                // First check is this cross is inside
                if (!Helpers.CurveInside(plot, crv))
                { continue; }
                // Make sure all offset "touch" the plot curve
                Curve offCrv1 = crv.Offset(Plane.WorldXY, AlleySpace, 0.001, CurveOffsetCornerStyle.Sharp)[0];
                Curve offCrv2 = crv.Offset(Plane.WorldXY, AlleySpace * -1, 0.001, CurveOffsetCornerStyle.Sharp)[0];

                cutters.Add(Helpers.ExtendToPlot(offCrv1, plot));
                cutters.Add(Helpers.ExtendToPlot(offCrv2, plot));
            }

            List<Brep> subSurf = Helpers.SplitAndSelect(surface, cutters, 0);
            #endregion


            Point3d[] divPT;
            courtyardCrv.DivideByLength(FL, false, false, out divPT);

            //Get splitting Lines
            List<Curve> splittingLines = new List<Curve>();
            if (divPT != null)
            {
                for (int j = 0; j < divPT.Length; j++)
                {
                    double plotOutlineT;
                    plot.ClosestPoint(divPT[j], out plotOutlineT);
                    splittingLines.Add(new Line(divPT[j], plot.PointAt(plotOutlineT)).ToNurbsCurve());
                }
            }

            // Split each footprint to sub footprints
            foreach (Brep srf in subSurf)
            {
                fp.AddRange(srf.Split(splittingLines, 0.0001));
            }


            return fp;
        }
        public List<Brep> Courtyard(Curve plot, double FD, double FL)
        {
            // Output
            List<Brep> fp = new List<Brep>();

            // Convert plot to polyline for Clipper offset
            Polyline plnPlot;
            plot.TryGetPolyline(out plnPlot);

            //if (!plnPlot.IsClosed)
            //{
            //    // Get the first point of the polyline
            //    Point3d firstPoint = plnPlot[0];
            //    // Add the first point to the end of the polyline
            //    plnPlot.Add(firstPoint);
            //    plot = plnPlot.ToNurbsCurve();
            //    Line line = new Line(plnPlot.First, plnPlot.Last);
            //    this.OpenLine = line;
            //}


            // Create internall courtyard curve
            List<Polyline> outputContours;
            List<Polyline> outputHoles;
            List<Polyline> plotList = new List<Polyline> { plnPlot };
            Polyline3D.Offset(plotList,
                Polyline3D.OpenFilletType.Square,
                Polyline3D.ClosedFilletType.Square,
                FD, Plane.WorldXY, 0.0001,
                out outputHoles, out outputContours);

            double debug = 0;

            Polyline courtyardPoly = outputContours[0];
            Curve courtyardCrv = courtyardPoly.ToNurbsCurve();

            Brep surface = Brep.CreatePlanarBreps(new List<Curve>() { plot, courtyardCrv }, 0.01)[0];

            Point3d[] divPT;
            courtyardCrv.DivideByLength(FL, false, false, out divPT);

            //Get splitting Lines
            List<Curve> splittingLines = new List<Curve>();
            if (divPT != null)
            {
                for (int j = 0; j < divPT.Length; j++)
                {
                    double plotOutlineT;
                    plot.ClosestPoint(divPT[j], out plotOutlineT);
                    splittingLines.Add(new Line(divPT[j], plot.PointAt(plotOutlineT)).ToNurbsCurve());
                }
            }

            fp = surface.Split(splittingLines, 0.01).ToList();

            this.PlnPlot = plnPlot;
            this.CourtyardPoly = courtyardPoly;

            return fp;
        }
        public List<Brep> OpenCourtyard(List<Brep> blds)
        {
            // Output
            List<Brep> fp = new List<Brep>();

            List<Line> segments = new List<Line>();

            Point3d midPoint = new Point3d();
            double minDistance = double.MaxValue;
            Point3d closestPoint = Point3d.Unset; // Output closest point
            int closestSegmentIndex = -1; // Output index of the closest segment
            List<int> indx = new List<int>();

            for (int i = 0; i < blds.Count; i++)
            {
                var box = blds[i].GetBoundingBox(false);
                midPoint = box.Center;

                for (int j = 0; j < PlnPlot.SegmentCount; j++)
                {

                    Line segment = PlnPlot.SegmentAt(j);

                    // Closest point on segment
                    double t = segment.ClosestParameter(midPoint);
                    Point3d ptOnSegment = segment.PointAt(t);

                    // Distance from input point to the closest point on segment
                    double distance = midPoint.DistanceTo(ptOnSegment);

                    // Update closest point and segment index if necessary
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestPoint = ptOnSegment;
                        closestSegmentIndex = j;

                    }
                }

                minDistance = double.MaxValue;

                if (closestSegmentIndex == 3)
                {
                    indx.Add(i);
                }
            }


            for (int i = 0; i < blds.Count; i++)
            {
                if (!indx.Contains(i))
                {
                    fp.Add(blds[i]);
                }

            }

            return fp;
        }
        public List<Brep> OpenCourtyard2(Curve plot, double FD, double FL)
        {
            // Output
            List<Brep> fp = new List<Brep>();

            List<Curve> outlinesPlot = new List<Curve>();

            Curve[] curves = plot.Offset(Plane.WorldXY, FD, 0.0001, CurveOffsetCornerStyle.Sharp);

            Curve offsetCrv = curves[0];

            // Convert plot to polyline for Clipper offset
            Polyline plnPlot;
            plot.TryGetPolyline(out plnPlot);

            Polyline courtyardPoly;
            offsetCrv.TryGetPolyline(out courtyardPoly);

            outlinesPlot.Add(curves[0]);
            outlinesPlot.Add(plot);

            NurbsSurface srf = NurbsSurface.CreateRuledSurface(plot, offsetCrv);

            Line end1 = new Line(plot.PointAtStart, offsetCrv.PointAtStart);
            Line end2 = new Line(plot.PointAtEnd, offsetCrv.PointAtEnd);
            Curve crv1 = end1.ToNurbsCurve();
            Curve crv2 = end2.ToNurbsCurve();

            Brep surface = Brep.CreatePlanarBreps(new List<Curve>() { plot, offsetCrv, crv1, crv2}, 0.01)[0];

            Point3d[] divPT;
            offsetCrv.DivideByLength(FL, false, false, out divPT);

            //Get splitting Lines
            List<Curve> splittingLines = new List<Curve>();
            if (divPT != null)
            {
                for (int j = 0; j < divPT.Length; j++)
                {
                    double plotOutlineT;
                    plot.ClosestPoint(divPT[j], out plotOutlineT);
                    splittingLines.Add(new Line(divPT[j], plot.PointAt(plotOutlineT)).ToNurbsCurve());
                }
            }

            fp = surface.Split(splittingLines, 0.01).ToList();

            this.PlnPlot = plnPlot;
            this.CourtyardPoly = courtyardPoly;

            return fp;
        }
        public List<Brep> Solid(Curve plot)
        {
            // Output
            List<Brep> fp = new List<Brep>();
            List<Brep> fpleft = new List<Brep>();

            Brep surface = Brep.CreatePlanarBreps(new List<Curve>() { plot }, 0.01)[0];
            Polyline plnPlot;
            plot.TryGetPolyline(out plnPlot);
            this.PlnPlot = plnPlot;

            //List<Brep> surfaces = new List<Brep>();
            //surfaces.Add(surface);

            Curve[] segments = plot.DuplicateSegments();
            // Sort by length in descending order
            Array.Sort(segments, (x, y) => y.GetLength().CompareTo(x.GetLength()));

            List<Line> longestEdges = new List<Line>();
            for (int i = 0; i < Math.Min(4, segments.Length); i++)
            {
                if (segments[i].TryGetPolyline(out Polyline polyline))
                {
                    longestEdges.Add(new Line(polyline[0], polyline[1]));
                }
            }

            Line longEdge1 = longestEdges[0];
            Line longEdge2 = longestEdges[1];
            Line shortEdge = longestEdges[2];
            Line shortEdgeMin = longestEdges[3];
            Curve crvLongEdge1 = longestEdges[0].ToNurbsCurve();
            Curve crvLongEdge2 = longestEdges[1].ToNurbsCurve();

            List<NurbsCurve> splittingLinesLong = new List<NurbsCurve>();

                double distance = (shortEdge.Length - MaxDepth) / 2;
                Point3d midPt1 = longEdge1.PointAt(0.5);
                Point3d midPt2 = longEdge2.PointAt(0.5);
                Vector3d vec1 = crvLongEdge1.TangentAt(0.5);
                Vector3d vec2 = crvLongEdge2.TangentAt(0.5);
                Vector3d perpendicularVector1 = Vector3d.CrossProduct(vec1, Vector3d.ZAxis);
                Vector3d perpendicularVector2 = Vector3d.CrossProduct(vec2, Vector3d.ZAxis);
                Vector3d translationVector1 = perpendicularVector1 * distance;
                Vector3d translationVector2 = perpendicularVector2 * distance;
                Transform translation1 = Transform.Translation(-translationVector1);
                Transform translation2 = Transform.Translation(-translationVector2);

                this.PerpVec = perpendicularVector1;

            //Check if footprints depth is larger then MaxDepth and if yes then trim it
            if (shortEdgeMin.Length > MaxDepth)
            {
                longEdge1.Transform(translation1);
                longEdge1.Extend(5, 5);
                longEdge2.Transform(translation2);
                longEdge2.Extend(5, 5);
                splittingLinesLong.Add(longEdge1.ToNurbsCurve());
                crvLongEdge1 = splittingLinesLong[0];
                splittingLinesLong.Add(longEdge2.ToNurbsCurve());
                crvLongEdge2 = splittingLinesLong[1];


                List<Brep> srfSplit = surface.Split(splittingLinesLong, 0.01).ToList();
                surface = srfSplit[0];

                Polyline plnPlotSmall;
                var crv = Helpers.getBrepEdges(surface);
                crv.TryGetPolyline(out plnPlotSmall);
                PlnPlot = plnPlotSmall;
            }


            List<Point3d> divPT = new List<Point3d>();
            List<double> randomNum = new List<double>();

            for (int i = 0; i < longEdge1.Length / MinLength; i++)
            {
                Random rnd = new Random((int)DateTime.Now.Ticks * i);
                double FL2 = MinLength + (rnd.NextDouble() * (RangeLength));
                randomNum.Add(FL2);
                double divAtLength = randomNum.Sum();

                if (divAtLength < longEdge1.Length && longEdge1.Length - divAtLength > MinLength)
                {
                    Point3d point = longEdge1.PointAtLength(divAtLength);
                    divPT.Add(point);
                }

                else
                {
                    break;
                }

            }

            //Get splitting Lines
            List<NurbsCurve> splittingLines = new List<NurbsCurve>();
            if (divPT != null)
            {
                for (int j = 0; j < divPT.Count; j++)
                {
                    double plotOutlineT;
                    crvLongEdge2.ClosestPoint(divPT[j], out plotOutlineT);
                    NurbsCurve divCrv = new Line(divPT[j], crvLongEdge2.PointAt(plotOutlineT)).ToNurbsCurve();
                    splittingLines.Add(divCrv);
                    this.FD = divCrv.GetLength();
                }
            }

            this.SplitLines = splittingLines;
            fp = surface.Split(splittingLines, 0.01).ToList();

            return fp;
        }
        public List<Brep> Tower(Curve plot)
        {
            // Output
            List<Brep> fp = new List<Brep>();
            List<Brep> fp2 = new List<Brep>();
            List<Polyline> polylineParts = new List<Polyline>();
            List<NurbsCurve> splittingLines = new List<NurbsCurve>();

            double minArea = 18 * 18;
            double maxArea = 25 * 25;

            Brep surface = Brep.CreatePlanarBreps(new List<Curve>() { plot }, 0.01)[0];

            Polyline pl;
            plot.TryGetPolyline(out pl);

            Random rnd = new Random((int)DateTime.Now.Ticks);
            // Generate a random double within the specified range
            double randomArea = minArea + (rnd.NextDouble() * (maxArea - minArea));

            AreaMassProperties amp = AreaMassProperties.Compute(plot.ToNurbsCurve());

            //double singlePartArea = amp.Area / (double)n;
            double singlePartArea = randomArea;

            double n = amp.Area / randomArea;

            Polyline remainingPoly = pl;

            List<Polyline> polyLeft = new List<Polyline>();

            for (int i = 0; i < n - 1; i++)
            {
                remainingPoly = split(remainingPoly, polylineParts, singlePartArea);
                polyLeft.AddRange(polylineParts);
            }
            polylineParts.Add(remainingPoly);

            foreach (var poly in polylineParts)
            {

                // Convert the polyline to a NURBS curve
                Curve nurbsCurve = poly.ToNurbsCurve();

                // Explode the NURBS curve into segments
                Curve[] explodedSegments = nurbsCurve.DuplicateSegments();

                foreach (Curve curve in explodedSegments)
                {
                    Point3d midPoint = curve.PointAtNormalizedLength(0.5); // Corrected midpoint calculation
                    double t;
                    plot.ClosestPoint(midPoint, out t);
                    Point3d closestPoint = plot.PointAt(t);
                    double distance = midPoint.DistanceTo(closestPoint);

                    if (distance > 0.1)
                    {
                        splittingLines.Add(curve.ToNurbsCurve());
                    }
                }

            }

            fp = surface.Split(splittingLines, 0.001).ToList();

            this.SplitLines = splittingLines;

            Random rnd2 = new Random((int)DateTime.Now.Ticks);
            // Generate a random double within the specified range
            int randomFp = (int)(rnd2.NextDouble() * fp.Count);

            for (int i = 0; i < fp.Count; i++)
            {
                if (fp[randomFp].GetArea() > (0.8 * minArea))
                {
                    fp2.Add(fp[randomFp]);
                    break;
                }

                randomFp++; // Increment randomFp for each iteration

                if (randomFp >= fp.Count) // If randomFp exceeds the count of fp, reset it to 0
                {
                    randomFp = 0;
                }

            }

            return fp2;
        }
        public List<Brep> RandomizeOffset(Polyline plot, Polyline courtyard, List<Brep> bldgs, double FD, double FL)
        {
            List<Polyline> outPlot;
            List<Polyline> outCrtydTemp;
            List<Polyline> outPlotTemp;
            List<Polyline> outCrtyd;
            List<Curve> splittingLines = new List<Curve>();

            List<Brep> surfaces = new List<Brep>();
            List<Brep> fpOffset = new List<Brep>();

            List<Polyline> outlinesPlot = new List<Polyline>();
            List<Polyline> outlinesCourtyard = new List<Polyline>();
            outlinesPlot.Add(plot);
            outlinesCourtyard.Add(courtyard);

            if (!plot.IsClosed)
            {
                // Get the first point of the polyline
                Point3d firstPoint = plot[0];
                // Add the first point to the end of the polyline
                plot.Add(firstPoint);
                Line line = new Line(plot.First, plot.Last);
                this.OpenLine = line;
            }

            if (!courtyard.IsClosed)
            {
                // Get the first point of the polyline
                Point3d firstPoint = courtyard[0];
                // Add the first point to the end of the polyline
                courtyard.Add(firstPoint);
                Line line = new Line(courtyard.First, courtyard.Last);
                this.OpenLine = line;
            }

            List<Polyline> plotOutline = new List<Polyline> { plot };
            List<Polyline> courtardOutline = new List<Polyline> { courtyard };

            List<int> number = new List<int>();
            for (int i = 0; i < MaxOff + 1; i = i + MinDiff)
            {

                Polyline3D.Offset(plotOutline,
                  Polyline3D.OpenFilletType.Square,
                  Polyline3D.ClosedFilletType.Miter,
                  i, Plane.WorldXY, 0.0001,
                  out outPlotTemp, out outPlot);
                outlinesPlot.AddRange(outPlot);

                Polyline3D.Offset(courtardOutline,
                  Polyline3D.OpenFilletType.Square,
                  Polyline3D.ClosedFilletType.Miter,
                  i, Plane.WorldXY, 0.0001,
                  out outCrtyd, out outCrtydTemp);
                outlinesCourtyard.AddRange(outCrtyd);

            }

            for (int i = 0; i < bldgs.Count; i++)
            {
                GH_Path path = new GH_Path(i);

                Random rnd1 = new Random((int)DateTime.Now.Ticks * i);
                int rndPlotOut = rnd1.Next(outlinesPlot.Count);
                int rndCrtydOut = rnd1.Next(outlinesCourtyard.Count);

                int check1 = rndPlotOut * MinDiff;
                int check2 = rndCrtydOut * MinDiff;

                if (FD - check1 < MinDepth) rndPlotOut = 0;
                if (FD - check2 < MinDepth) rndCrtydOut = 0;
                if (FD - (check1 + check2) < MinDepth)
                {
                    rndPlotOut = 0;
                    rndCrtydOut = 0;
                }

                Polyline randomLine1 = outlinesPlot[rndPlotOut];
                splittingLines.Add(randomLine1.ToNurbsCurve());

                Polyline randomLine2 = outlinesCourtyard[rndCrtydOut];
                splittingLines.Add(randomLine2.ToNurbsCurve());


                try
                {
                    surfaces = bldgs[i].Split(splittingLines, 0.01).ToList();
                    splittingLines.Clear();
                    if (surfaces.Count > 2) fpOffset.Add(surfaces[2]);
                    else fpOffset.Add(surfaces[1]);
                }

                catch
                {
                    fpOffset.Add(surfaces[0]);
                }

  
            }


            return fpOffset;
        }
        public List<Brep> RandomizeOffsetSolid(Polyline plot, List<Brep> bldgs, double FD)
        {
            List<Polyline> outPlot;
            List<Polyline> outCrtydTemp;

            List<Curve> splittingLines = new List<Curve>();

            List<Brep> surfaces = new List<Brep>();
            List<Brep> fpOffset = new List<Brep>();

            List<Polyline> outlinesPlot = new List<Polyline>();
            outlinesPlot.Add(plot);

            List<Polyline> plotOutline = new List<Polyline> { plot };

            List<int> number = new List<int>();
            for (int i = 0; i < MaxOff + 1; i = i + MinDiff)
            {

                Polyline3D.Offset(plotOutline,
                  Polyline3D.OpenFilletType.Square,
                  Polyline3D.ClosedFilletType.Miter,
                  i, Plane.WorldXY, 0.0001,
                  out outCrtydTemp, out outPlot);
                outlinesPlot.AddRange(outPlot);
            }

            Random rnd1 = new Random((int)DateTime.Now.Ticks);
            int rndPlotOut = rnd1.Next(outlinesPlot.Count);
            int check3 = rndPlotOut * MinDiff;

            for (int i = 0; i < bldgs.Count; i++)
            {
                GH_Path path = new GH_Path(i);

                if (FD - 2 * check3 < MinDepth)
                {
                    rndPlotOut = 0;
                    check3 = rndPlotOut * MinDiff;
                }

                Polyline randomLine1 = outlinesPlot[rndPlotOut];
                splittingLines.Add(randomLine1.ToNurbsCurve());

                try
                {
                    surfaces = bldgs[i].Split(splittingLines, 0.01).ToList();
                    splittingLines.Clear();
                    if (surfaces.Count > 2) fpOffset.Add(surfaces[2]);
                    else fpOffset.Add(surfaces[1]);
                }

                catch
                {
                    fpOffset.Add(surfaces[0]);
                }
            }

            //Shift bldgs
            for (int i = 0; i < fpOffset.Count; i++)
            {
                Random rnd = new Random((int)DateTime.Now.Ticks * i);
                int rndShift = rnd.Next(check3);
                rndShift = (int)Math.Round((double)rndShift / MinDiff) * MinDiff;

                Vector3d movement = PerpVec * rndShift;
                Transform translation = Transform.Translation(movement);
                fpOffset[i].Transform(translation);
            }


            return fpOffset;
        }
    }
}
