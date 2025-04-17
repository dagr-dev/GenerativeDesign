using System;
using System.Collections.Generic;
using Generative_Urban_prj.Classes;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel.Data;

namespace Generative_Urban_prj.Components
{
    public class FloorGeneratorSteps : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public FloorGeneratorSteps()
          : base("3DgeneratorSteps", "3DgeneratorSteps",
              "Description",
              "HLA_GD", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Footprints", "Footprints", "Footprints", GH_ParamAccess.list);
            pManager.AddBrepParameter("Tower Footprints", "TowFootprints", "Individual Tower Footprints", GH_ParamAccess.list);
            pManager.AddIntegerParameter("FP indices", "Indices", "Footprint indices from tree", GH_ParamAccess.list);
            pManager.AddNumberParameter("Plot area", "Plots area", "Plots area", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Min", "Min", "Min", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Max", "Max", "Max", GH_ParamAccess.list);
            pManager.AddIntegerParameter("MinTow", "MinTow", "Min num floor for tower", GH_ParamAccess.item);
            pManager.AddIntegerParameter("MaxTow", "MaxTow", "Max num floor for tower", GH_ParamAccess.item);
            pManager.AddIntegerParameter("FAR", "FAR", "FAR", GH_ParamAccess.item);
            pManager.AddNumberParameter("FloorHeight", "FloorHeight", "FloorHeight", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Tower?", "Tower?", "Add a tower to the courtyard?", GH_ParamAccess.item);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Floors", "Floors", "Floors", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Area", "Area", "Area", GH_ParamAccess.tree);
            pManager.AddBrepParameter("Floors towers", "FloorsTow", "Floors towers", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Area towers", "AreaTow", "Area towers", GH_ParamAccess.tree);

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            #region Get inputs
            List<int> min = new List<int>();
            List<int> max = new List<int>();
            int minTow = 0;
            int maxTow = 0;
            int far = 0;
            double height = 0;
            List<Brep> fp = new List<Brep>();
            List<Brep> fpTow = new List<Brep>();
            List<int> indices = new List<int>();
            double plotArea = 0.0;
            bool tower = false;

            DA.GetDataList(0, fp);
            DA.GetDataList(1, fpTow);
            DA.GetDataList(2, indices);
            DA.GetData(3, ref plotArea);
            DA.GetDataList(4, min);
            DA.GetDataList(5, max);
            DA.GetData(6, ref minTow);
            DA.GetData(7, ref maxTow);
            DA.GetData(8, ref far);
            DA.GetData(9, ref height);
            DA.GetData(10, ref tower);
            #endregion

            Floors floors = new Floors(fp, fpTow, indices, plotArea, min, max, minTow, maxTow, far, height, tower);

            foreach (string mes in floors.messages)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, mes);
            }

            DA.SetDataTree(0, floors.FloorsOut);
            DA.SetDataTree(1, floors.GFA);
            DA.SetDataTree(2, floors.FloorsTowerOut);
            DA.SetDataTree(3, floors.GFAtow);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("748B9890-470E-4AAD-8876-CD1E8AA05EB5"); }
        }
    }
}