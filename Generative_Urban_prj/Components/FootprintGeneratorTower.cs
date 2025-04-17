using System;
using System.Collections.Generic;
using Generative_Urban_prj.Classes;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Generative_Urban_prj.Components
{
    public class FootprintGeneratorTower : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public FootprintGeneratorTower()
          : base("FootprintGeneratorTower", "Footprints",
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
            List<Curve> plots = new List<Curve>();

            DA.GetDataList(0, plots);
            #endregion

            Footprints fp = new Footprints(plots);

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
            get { return new Guid("50FF00E2-F050-42EC-BDE3-28A743C45680"); }
        }
    }
}