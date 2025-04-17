using System;
using System.Collections.Generic;
using Generative_Urban_prj.Classes;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Generative_Urban_prj.Components
{
    public class FootprintGeneratorRowHouses : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public FootprintGeneratorRowHouses()
          : base("FootprintGeneratorRowHouses", "Footprints",
              "Description",
              "HLA_GD", "Subcategory")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Plots", "Plots", "Plots", GH_ParamAccess.list);
            pManager.AddNumberParameter("minDepth", "minDepth", "minDepth", GH_ParamAccess.item);
            pManager.AddNumberParameter("maxDepth", "maxDepth", "maxDepth", GH_ParamAccess.item);
            pManager.AddNumberParameter("minLepth", "minLepth", "minLepth", GH_ParamAccess.item);
            pManager.AddNumberParameter("maxLepth", "maxLepth", "maxLepth", GH_ParamAccess.item);
            pManager.AddIntegerParameter("maxOff", "maxOff", "maxOff", GH_ParamAccess.item);
            pManager.AddIntegerParameter("minDiff", "minDiff", "minDiff", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Footprints", "Footprints", "Footprints", GH_ParamAccess.tree);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            #region Get inputs
            double minD = 0.0;
            double maxD = 0.0;
            double minL = 0.0;
            double maxL = 0.0;
            int maxOff = 0;
            int minDiff = 0;
            int typo = 0;
            List<Curve> plots = new List<Curve>();

            DA.GetDataList(0, plots);
            DA.GetData(1, ref minD);
            DA.GetData(2, ref maxD);
            DA.GetData(3, ref minL);
            DA.GetData(4, ref maxL);
            DA.GetData(5, ref maxOff);
            DA.GetData(6, ref minDiff);
            #endregion

            Footprints fp = new Footprints(minD, maxD, minL, maxL, minDiff, maxOff, plots);

            DA.SetDataTree(0, fp.FootprintsOut);
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
            get { return new Guid("1FE76676-6E01-4DD4-A027-EF106F21C11F"); }
        }
    }
}