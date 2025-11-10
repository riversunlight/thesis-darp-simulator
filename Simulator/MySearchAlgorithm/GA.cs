using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Google.OrTools.ConstraintSolver;
using Google.OrTools.Sat;
using Simulator.Objects.Data_Objects.Routing;
using Simulator.Objects.Simulation;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Simulator.MySearchAlgorithm
{

　　// 簡単化のため一旦GAにする(目的関数は1個目にしよう)

    public class GA : BaseGA
    {
        // Vianaを参考にしよう
        public GA(RoutingModel _routingModel, RoutingIndexManager _manager, RoutingDataModel _DataModel):base(_routingModel, _manager, _DataModel)
        {
        }        

        public override MyAssignment TryGetSolution()
        {
            sw = Stopwatch.StartNew();

            MyAssignment dummy = new MyAssignment(0);
            dummy.setPath("GA.csv");
            dummy.setGeneCnt(0);

            InitialPopulation();

            while(StoppingCondition() == 0)
            {
                CrossOver();
                for (int i = 0; i < offspring_size; i++) offspring[i].Simulate(DataModel);
                SelectSurvivor();
                generationCnt++;
                dummy.setGeneCnt(generationCnt);
            }
            MyAssignment solution = population[0];

            for (int i = 0; i < population_size; i++)
            {
                population[i].VisualTextSimulateResult(i, "GA");
            }


            return solution;
        }
    }
}
