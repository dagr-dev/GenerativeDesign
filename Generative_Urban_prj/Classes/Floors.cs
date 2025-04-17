using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ClipperLib;
using Grasshopper;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Rhino.Input.Custom;
using static Rhino.DocObjects.PhysicallyBasedMaterial;

namespace Generative_Urban_prj.Classes
{
    public class Floors
    {
        Random rnd = new Random();

        public DataTree<double> GFA = new DataTree<double>();
        public DataTree<Brep> FloorsOut = new DataTree<Brep> ();
        public DataTree<double> GFAtow = new DataTree<double>();
        public DataTree<Brep> FloorsTowerOut = new DataTree<Brep>();
        public List<string> messages = new List<string> ();

        public Floors(List<Brep> footprints, List<Brep> footprintsTow, List<int> indices, double plotArea, int min, int max, int minTow, int maxTow, int far, double height, bool tower)
        {
            FloorsOut.Clear ();
            GFA.Clear ();

            double totalArea = 0;
            int plotCount = indices.Distinct().Count();

            #region figure out which footprints can be towers
            // Figure out which footprints can be towers (corners)
            List<int>[] indicesCorner = new List<int>[plotCount];
            for (int i = 0; i < footprints.Count; i++)
            {
                int numEdges = footprints[i].Edges.Count;
                // if it's a corner footprint
                if(numEdges > 4)
                {
                    if(indicesCorner[indices[i]] == null)
                    {
                        indicesCorner[indices[i]] = new List<int> { i };
                    }
                    else
                        indicesCorner[indices[i]].Add(i);
                }
            }
            List<int> selectedTowers = new List<int>();
            // Select only one tower footprint per block
            foreach (List<int> indexList in indicesCorner)
            {
                if (indexList != null && indexList.Count > 0)
                {
                    int randomIndex = rnd.Next(indexList.Count);
                    selectedTowers.Add(indexList[randomIndex]);
                }
            }
            #endregion

            List<GH_Path> towersPath = new List<GH_Path>();
            // First fill all the min except individual towers
            for (int i = 0; i < footprints.Count; i++)
            {
                GH_Path path = new GH_Path(new int[] { indices[i], i });
                int minNumFloor = min;
                // if the input bool tower is true and the footprint is a corner one
                if(tower && selectedTowers.Contains(i))
                {
                    minNumFloor = minTow;
                    towersPath.Add(path);
                }
                double area;
                List<Brep> floorTemp = PopulateFp(footprints[i], minNumFloor, height, out area);
                FloorsOut.AddRange(floorTemp, path);
                totalArea += area;
                GFA.Add(area, path);
            }

            // Fill the min for individual towers
            for (int i = 0; i < footprintsTow.Count; i++)
            {
                GH_Path path = new GH_Path(i);
                double area;
                List<Brep> floorTemp = PopulateFp(footprintsTow[i], minTow, height, out area);
                FloorsTowerOut.AddRange(floorTemp, path);
                totalArea += area;
                GFAtow.Add(area, path);
            }

            double currentFAR = (totalArea / plotArea) * 100;
            // Check if all buildings have the max num floors
            int maximized = 0;
            List<int> maximizedIndices = new List<int>();
            int maximizedTow = 0;
            List<int> maximizedIndicesTow = new List<int>();

            while (currentFAR < far && maximized < FloorsOut.BranchCount)
            {
                // Choose a random branch between both trees
                int iBranch = rnd.Next(FloorsOut.BranchCount + FloorsTowerOut.BranchCount);

                if (iBranch >= FloorsOut.BranchCount && maximizedTow < FloorsTowerOut.BranchCount)
                {
                    iBranch -= FloorsOut.BranchCount;
                    GH_Path path = FloorsTowerOut.Path(iBranch);

                    List<Brep> floors = FloorsTowerOut.Branch(iBranch);
                    // if it hasn't exceeded the max floor count
                    if (floors.Count <= maxTow)
                    {
                        // save the previous number of floors
                        int numFloors = floors.Count;
                        // Add one more floor area to the total
                        double floorArea = GFAtow.Branch(iBranch)[0] / numFloors;
                        totalArea += floorArea;
                        // Update GFA output
                        GFAtow.Branch(iBranch)[0] += floorArea;
                        // ACtually add the new floor Brep to the tree
                        FloorsTowerOut.Add(AddFloor(floors, height), path);
                        currentFAR = (totalArea / plotArea) * 100;
                    }
                    else if (!maximizedIndicesTow.Contains(iBranch)) // if it has reached the max num floors
                    {
                        maximizedIndicesTow.Add(iBranch);
                        maximizedTow++;
                    }
                }
                else
                {
                    GH_Path path = FloorsOut.Path(iBranch);

                    int maxNumFloors = max;
                    if (towersPath.Contains(path))
                        maxNumFloors = maxTow;

                    List<Brep> floors = FloorsOut.Branch(iBranch);
                    // if it hasn't exceeded the max floor count
                    if (floors.Count <= maxNumFloors)
                    {
                        // save the previous number of floors
                        int numFloors = floors.Count;
                        // Add one more floor area to the total
                        double floorArea = GFA.Branch(iBranch)[0] / numFloors;
                        totalArea += floorArea;
                        // Update GFA output
                        GFA.Branch(iBranch)[0] += floorArea;
                        // ACtually add the new floor Brep to the tree
                        FloorsOut.Add(AddFloor(floors, height), path);
                        currentFAR = (totalArea / plotArea) * 100;
                    }
                    else if (!maximizedIndices.Contains(iBranch)) // if it has reached the max num floors
                    {
                        maximizedIndices.Add(iBranch);
                        maximized++;
                    }
                }

            }

            if (currentFAR < far)
                messages.Add("FAR not reached");
        }


        public Floors(List<Brep> footprints, List<Brep> footprintsTow, List<int> indices, double plotArea, List<int> min, List<int> max, int minTow, int maxTow, int far, double height, bool tower) 
        {
            FloorsOut.Clear();
            GFA.Clear();

            double totalArea = 0;
            int plotCount = indices.Distinct().Count();

            #region figure out which footprints can be towers
            // Figure out which footprints can be towers (corners)
            List<int>[] indicesCorner = new List<int>[plotCount];
            for (int i = 0; i < footprints.Count; i++)
            {
                int numEdges = footprints[i].Edges.Count;
                // if it's a corner footprint
                if (numEdges > 4)
                {
                    if (indicesCorner[indices[i]] == null)
                    {
                        indicesCorner[indices[i]] = new List<int> { i };
                    }
                    else
                        indicesCorner[indices[i]].Add(i);
                }
            }
            List<int> selectedTowers = new List<int>();
            // Select only one tower footprint per block
            foreach (List<int> indexList in indicesCorner)
            {
                if (indexList != null && indexList.Count > 0)
                {
                    int randomIndex = rnd.Next(indexList.Count);
                    selectedTowers.Add(indexList[randomIndex]);
                }
            }
            #endregion

            List<GH_Path> towersPath = new List<GH_Path>();
            // First fill all the min except individual towers
            for (int i = 0; i < footprints.Count; i++)
            {
                GH_Path path = new GH_Path(new int[] { indices[i], i });

                int minLocal = min[i];

                int minNumFloor = minLocal;
                // if the input bool tower is true and the footprint is a corner one
                if (tower && selectedTowers.Contains(i))
                {
                    minNumFloor = minTow;
                    towersPath.Add(path);
                }
                double area;
                List<Brep> floorTemp = PopulateFp(footprints[i], minNumFloor, height, out area);
                FloorsOut.AddRange(floorTemp, path);
                totalArea += area;
                GFA.Add(area, path);
            }

            // Fill the min for individual towers
            for (int i = 0; i < footprintsTow.Count; i++)
            {
                GH_Path path = new GH_Path(i);
                double area;
                List<Brep> floorTemp = PopulateFp(footprintsTow[i], minTow, height, out area);
                FloorsTowerOut.AddRange(floorTemp, path);
                totalArea += area;
                GFAtow.Add(area, path);
            }

            double currentFAR = (totalArea / plotArea) * 100;
            // Check if all buildings have the max num floors
            int maximized = 0;
            List<int> maximizedIndices = new List<int>();
            int maximizedTow = 0;
            List<int> maximizedIndicesTow = new List<int>();

            while (currentFAR < far && maximized < FloorsOut.BranchCount)
            {
                // Choose a random branch between both trees
                int iBranch = rnd.Next(FloorsOut.BranchCount + FloorsTowerOut.BranchCount);

                // if it's a tower
                if (iBranch >= FloorsOut.BranchCount && maximizedTow < FloorsTowerOut.BranchCount)
                {
                    iBranch -= FloorsOut.BranchCount;
                    GH_Path path = FloorsTowerOut.Path(iBranch);

                    List<Brep> floors = FloorsTowerOut.Branch(iBranch);
                    // if it hasn't exceeded the max floor count
                    if (floors.Count <= maxTow)
                    {
                        // save the previous number of floors
                        int numFloors = floors.Count;
                        // Add one more floor area to the total
                        double floorArea = GFAtow.Branch(iBranch)[0] / numFloors;
                        totalArea += floorArea;
                        // Update GFA output
                        GFAtow.Branch(iBranch)[0] += floorArea;
                        // ACtually add the new floor Brep to the tree
                        FloorsTowerOut.Add(AddFloor(floors, height), path);
                        currentFAR = (totalArea / plotArea) * 100;
                    }
                    else if (!maximizedIndicesTow.Contains(iBranch)) // if it has reached the max num floors
                    {
                        maximizedIndicesTow.Add(iBranch);
                        maximizedTow++;
                    }
                }
                else
                {
                    GH_Path path = FloorsOut.Path(iBranch);

                    int maxNumFloors = max[iBranch];
                    if (towersPath.Contains(path))
                        maxNumFloors = maxTow;

                    List<Brep> floors = FloorsOut.Branch(iBranch);
                    // if it hasn't exceeded the max floor count
                    if (floors.Count <= maxNumFloors)
                    {
                        // save the previous number of floors
                        int numFloors = floors.Count;
                        // Add one more floor area to the total
                        double floorArea = GFA.Branch(iBranch)[0] / numFloors;
                        totalArea += floorArea;
                        // Update GFA output
                        GFA.Branch(iBranch)[0] += floorArea;
                        // ACtually add the new floor Brep to the tree
                        FloorsOut.Add(AddFloor(floors, height), path);
                        currentFAR = (totalArea / plotArea) * 100;
                    }
                    else if (!maximizedIndices.Contains(iBranch)) // if it has reached the max num floors
                    {
                        maximizedIndices.Add(iBranch);
                        maximized++;
                    }
                }

            }

            if (currentFAR < far)
                messages.Add("FAR not reached");
        }
        List<Brep> PopulateFp(Brep fp, int num, double floorH, out double area)
        {
            List<Brep> floors = new List<Brep>();
            double areaUnit = fp.GetArea();
            double areaSum = 0;

            Point3d ptStart = Helpers.getBrepEdges(fp).PointAtStart;
            Curve pathCrv = new Line(ptStart, new Point3d(ptStart.X, ptStart.Y, floorH)).ToNurbsCurve();
            Brep extruded = fp.Faces[0].CreateExtrusion(pathCrv, true);

            for (int i = 0; i < num; i++)
            {
                Brep next = extruded.DuplicateBrep();
                next.Translate(new Vector3d(0, 0, i * floorH));
                floors.Add(next);
                areaSum += areaUnit;
            }

            area = areaSum;
            return floors;
        }

        Brep AddFloor(List<Brep> floors, double floorH) 
        {
            int index = floors.Count - 1;
            Brep next = floors[index].DuplicateBrep();
            next.Translate(new Vector3d(0, 0, floorH));
            return next;
        }


    }
}
