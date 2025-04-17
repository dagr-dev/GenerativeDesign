using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClipperLib;

namespace Generative_Urban_prj
{
    public static class Helpers
    {
        public static Curve getBrepEdges(Brep brep)
        {
            List<Curve> edges = new List<Curve>();
            for (int i = 0; i < brep.Edges.Count; i++)
            {
                edges.Add(brep.Edges[i].EdgeCurve);
            }
            return Curve.JoinCurves(edges)[0];
        }

        public static Curve ExtendToPlot(Curve crv, Curve plot)
        {
            // Find the side of the curve that is closer to the plot
            Point3d start = crv.PointAtStart;
            Point3d end = crv.PointAtEnd;
            double t;

            //plot.ClosestPoint(start, out t);
            //double distanceStart = start.DistanceTo(plot.PointAt(t));

            //plot.ClosestPoint(end, out t);
            //double distanceEnd = end.DistanceTo(plot.PointAt(t));

            //CurveEnd side = CurveEnd.Start;
            //if (distanceEnd < distanceStart)
            //    side = CurveEnd.End;

            Curve crvOne = crv.Extend(CurveEnd.Start, CurveExtensionStyle.Line, new List<Curve> { plot });
            if(crvOne != null)
            {
                Curve crvNew = crvOne.Extend(CurveEnd.End, CurveExtensionStyle.Line, new List<Curve> { plot });
                if (crvNew == null)
                    return crvOne;
                else
                    return crvNew;
            }
            else
            {
                Curve crvNew = crv.Extend(CurveEnd.End, CurveExtensionStyle.Line, new List<Curve> { plot });
                if (crvNew != null)
                    return crvNew;
                else
                    return crv;
            }

        }

        public static List<double> AddIntersections(Curve crv, Curve plot, List<double> intersections)
        {
            Rhino.Geometry.Intersect.CurveIntersections crvInter = Rhino.Geometry.Intersect.Intersection.CurveCurve(plot.ToNurbsCurve(), crv, 0.01, 0.01);
            //IEnumerator<Rhino.Geometry.Intersect.IntersectionEvent> events = crvInter.GetEnumerator();

            foreach (var inter in crvInter)
            {
                intersections.Add(inter.ParameterA);
            }

            return intersections;
        }

        public static Curve OffsetPlot(Curve plot, double FD, bool inside)
        {
            // Convert plot to polyline for Clipper offset
            Polyline plnPlot;
            plot.TryGetPolyline(out plnPlot);

            List<Polyline> outputContours;
            List<Polyline> outputHoles;
            List<Polyline> plotList = new List<Polyline> { plnPlot };
            Polyline3D.Offset(plotList,
                Polyline3D.OpenFilletType.Square,
                Polyline3D.ClosedFilletType.Square,
                FD, Plane.WorldXY, 0.0001,
                out outputHoles, out outputContours);

            Polyline courtyardPoly = new Polyline();
            if (inside)
            { 
                courtyardPoly = outputContours[0];
            }
            else
            { 
                courtyardPoly = outputHoles[0];
            }

            Curve courtyardCrv = courtyardPoly.ToNurbsCurve();

            return courtyardCrv;
        }

        public static bool CurveInside(Curve plot, Curve crvToCheck)
        {
            bool inside = false;

            if(plot.Contains(crvToCheck.PointAt(0.5), Plane.WorldXY, 0.0001)== PointContainment.Inside)
                inside = true;

            return inside;
        }

        public static List<Brep> SplitAndSelect(Brep surface, List<Curve> cutters, int count)
        {
            List<Brep> cutterBreps = cutters.Select(cutter => Extrusion.Create(cutter, 10, false).ToBrep()).ToList();

            Brep[] splitted = surface.Split(cutterBreps, 0.001);
            
            return splitted.OrderByDescending(brep => brep.GetArea()).Take(4).ToList(); ;
        }

    }
}
