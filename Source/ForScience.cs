using KSP.UI.Screens;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ToolbarControl_NS;

using static ForScience.InitLog;

namespace ForScience
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class ForScience : MonoBehaviour
    {
        //GUI

        //states
        Vessel stateVessel;
        CelestialBody stateBody;
        string stateBiome;
        ExperimentSituations stateSituation = 0;
        bool newScene = false;

        //thread control
        bool autoTransfer = true;

        ToolbarControl toolbarControl = null;


        void Awake()
        {
            SetupAppButton();
        }

        void Start()
        {
            newScene = true;
        }

        void OnDestroy()
        {
            toolbarControl.OnDestroy();
            Destroy(toolbarControl);
            toolbarControl = null;

        }

        internal const string MODID = "ForScienceNS";
        internal const string MODNAME = "For Science!";

        void SetupAppButton()
        {
            if (toolbarControl == null)
            {
                toolbarControl = gameObject.AddComponent<ToolbarControl>();
                toolbarControl.AddToAllToolbars(ToggleCollection, ToggleCollection,
                    ApplicationLauncher.AppScenes.FLIGHT,
                    MODID,
                    "ForScienceButton",
                    "ForScience/Icons/FS_active",
                    "ForScience/Icons/FS_active",
                    MODNAME
                );
            }

        }

        double lastCheck = 0;
        /// <summary>
        /// Do the science checks while running in physics update so that the vessel is always in a valid state to check for science.
        /// </summary>
        void FixedUpdate()
        {
            // Only do the code below 2x a second
            var time = Time.realtimeSinceStartup;
            if (time - lastCheck < 0.5f)
                return;
            lastCheck = time;

            // this is the primary logic that controls when to do what, so we aren't contstantly eating cpu
            if (FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>().Any() == false)
            {
                // Check if any science containers are on the vessel, if not, remove the app button
                toolbarControl.buttonActive = false;
                return;
            }
            if (HighLogic.CurrentGame.Mode == Game.Modes.CAREER | HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX) // only modes with science mechanics will run
            {
                toolbarControl.buttonActive = true;
                if (autoTransfer) // if we've enabled the app to run, on by default, the toolbar button toggles this.
                {
                    TransferScience();// always move experiment data to science container, mostly for manual experiments
                    if (StatesHaveChanged()) // if we are in a new state, we will check and run experiments
                    {
                        RunScience();
                    }
                }
            }

        }

        /// <summary>
        /// automaticlly find, transer and consolidate science data on the vessel
        /// </summary>
        void TransferScience()
        {
            if (ActiveContainer().GetActiveVesselDataCount() != ActiveContainer().GetScienceCount()) // only actually transfer if there is data to move
            {

                Log.Info("Transfering science to container.");

                var scienceExps = GetExperimentList();
                List<IScienceDataContainer> scienceContainers = new List<IScienceDataContainer>();
                for (int i = 0; i < scienceExps.Count; i++)
                {
                    if (scienceExps[i].rerunnable || IsScientistOnBoard)
                        scienceContainers.Add(scienceExps[i]);
                }
                ActiveContainer().StoreData(scienceContainers, true);

                //ActiveContainer().StoreData(GetExperimentList().Cast<IScienceDataContainer>().ToList(), true); // this is what actually moves the data to the active container
                var containerstotransfer = GetContainerList(); // a temporary list of our containers
                containerstotransfer.Remove(ActiveContainer()); // we need to remove the container we storing the data in because that would be wierd and buggy
                ActiveContainer().StoreData(containerstotransfer.Cast<IScienceDataContainer>().ToList(), true); // now we store all data from other containers
            }
        }

        /// <summary>
        /// this is primary business logic for finding and running valid experiments
        /// </summary>
        void RunScience() 
        {
            if (GetExperimentList() == null) // hey, it can happen!
            {
                Log.Info("There are no experiments.");
            }
            else
            {
                foreach (ModuleScienceExperiment currentExperiment in GetExperimentList()) // loop through all the experiments onboard
                {
                    Log.Info("Checking experiment: " + currentExperiment.experimentID +
                        ", " + CurrentScienceSubject(currentExperiment.experiment).id +
                        ", " + CurrentScienceSubject(currentExperiment.experiment).title);

                    if (ActiveContainer().HasData(NewScienceData(currentExperiment))) // skip data we already have onboard
                    {

                        Log.Info("Skipping: We already have that data onboard.");

                    }
                    else if (!SurfaceSamplesUnlocked() && currentExperiment.experiment.id == "surfaceSample") // check to see is surface samples are unlocked
                    {
                        Log.Info("Skipping: Surface Samples are not unlocked.");
                    }
                    else if (!currentExperiment.rerunnable && !IsScientistOnBoard) // no cheating goo and materials here
                    {

                        Log.Info("Skipping: Experiment is not repeatable.");

                    }
                    else if (!currentExperiment.experiment.IsAvailableWhile(CurrentSituation(), CurrentBody())) // this experiement isn't available here so we skip it
                    {

                        Log.Info("Skipping: Experiment is not available for this situation/atmosphere.");
                        Log.Info("CurrentSituation: " + CurrentSituation() + ", CurrentBody: " + CurrentBody().bodyName);
                        Log.Info("Situationmask: " + currentExperiment.experiment.situationMask + ", BiomeMask: " +
                            currentExperiment.experiment.biomeMask);
                    }
                    // TODO - Science Labs can use zero value science , so do not skip it if there is a lab on board
                    // as a temporary workaround, if there is a scientist on board it will still gather the data.

                    else if (CurrentScienceValue(currentExperiment) >= 0.1 || IsScientistOnBoard)
                    {

                        Log.Info("Running experiment: " + CurrentScienceSubject(currentExperiment.experiment).id);

                        //manually add data to avoid deployexperiment state issues
                        ActiveContainer().AddData(NewScienceData(currentExperiment));

                    }
                    else // this experiment has no more value so we skip it
                    {
                        Log.Info("Skipping: No more science is available: ");
                    }

                }
            }
        }

        /// <summary>
        /// checking that the appropriate career unlocks are flagged
        /// </summary>
        /// <returns></returns>
        private bool SurfaceSamplesUnlocked() 
        {
            return GameVariables.Instance.UnlockedEVA(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex))
                && GameVariables.Instance.UnlockedFuelTransfer(ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment));
        }

        /// <summary>
        /// the ammount of science an experiment should return
        /// </summary>
        /// <param name="currentExperiment"></param>
        /// <returns></returns>
        float CurrentScienceValue(ModuleScienceExperiment currentExperiment)
        {
            return ResearchAndDevelopment.GetScienceValue(
                                    currentExperiment.experiment.baseValue * currentExperiment.experiment.dataScale,
                                    CurrentScienceSubject(currentExperiment.experiment));
        }

        /// <summary>
        /// construct our own science data for an experiment
        /// </summary>
        /// <param name="currentExperiment"></param>
        /// <returns></returns>
        ScienceData NewScienceData(ModuleScienceExperiment currentExperiment) 
        {
            return new ScienceData(
                       amount: currentExperiment.experiment.baseValue * CurrentScienceSubject(currentExperiment.experiment).dataScale,
                       xmitValue: currentExperiment.xmitDataScalar,
                       xmitBonus: 0f,
                       id: CurrentScienceSubject(currentExperiment.experiment).id,
                       dataName: CurrentScienceSubject(currentExperiment.experiment).title
                       );
        }

        Vessel CurrentVessel() // dur :P
        {
            return FlightGlobals.ActiveVessel;
        }

        CelestialBody CurrentBody()
        {
            return FlightGlobals.ActiveVessel.mainBody;
        }

        ExperimentSituations CurrentSituation()
        {
            return ScienceUtil.GetExperimentSituation(FlightGlobals.ActiveVessel);
        }

        /// <summary>
        /// some crazy nonsense to get the actual biome string
        /// </summary>
        /// <returns></returns>
        string CurrentBiome() 
        {
            if (FlightGlobals.ActiveVessel != null)
                if (FlightGlobals.ActiveVessel.mainBody.BiomeMap != null)
                    return !string.IsNullOrEmpty(FlightGlobals.ActiveVessel.landedAt)
                                    ? Vessel.GetLandedAtString(FlightGlobals.ActiveVessel.landedAt)
                                    : ScienceUtil.GetExperimentBiome(FlightGlobals.ActiveVessel.mainBody,
                                                FlightGlobals.ActiveVessel.latitude, FlightGlobals.ActiveVessel.longitude);

            return string.Empty;
        }

        ScienceSubject CurrentScienceSubject(ScienceExperiment experiment)
        {
            string fixBiome = string.Empty; // some biomes don't have 4th string, so we just put an empty in to compare strings later
            if (experiment.BiomeIsRelevantWhile(CurrentSituation())) fixBiome = CurrentBiome();// for those that do, we add it to the string
            return ResearchAndDevelopment.GetExperimentSubject(experiment, CurrentSituation(), CurrentBody(), fixBiome, null);//ikr!, we pretty much did all the work already, jeez
        }

        /// <summary>
        /// set the container to gather all science data inside, usualy this is the root command pod of the oldest vessel
        /// </summary>
        /// <returns></returns>
        ModuleScienceContainer ActiveContainer() 
        {
            return FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>().FirstOrDefault();
        }

        /// <summary>
        /// a list of all experiments
        /// </summary>
        /// <returns></returns>
        List<ModuleScienceExperiment> GetExperimentList()
        {
            return FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceExperiment>();
        }

        /// <summary>
        /// a list of all science containers
        /// </summary>
        /// <returns></returns>
        List<ModuleScienceContainer> GetContainerList() 
        {
            return FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>(); // list of all experiments onboard
        }

        /// <summary>
        /// Track our vessel state, it is used for thread control to know when to fire off new experiments since there is no event for this
        /// </summary>
        /// <returns></returns>
        bool StatesHaveChanged()
        {
            if (CurrentVessel() != stateVessel ||
                CurrentSituation() != stateSituation ||
                CurrentBody() != stateBody ||
                CurrentBiome() != stateBiome ||
                newScene)
            {
                stateVessel = CurrentVessel();
                stateBody = CurrentBody();
                stateSituation = CurrentSituation();
                stateBiome = CurrentBiome();
                newScene = false;

                return true;
            }
            else return false;
        }

        /// <summary>
        /// This is our main toggle for the logic and changes the icon between green and red versions on the bar when it does so.
        /// </summary>
        void ToggleCollection() 
        {
            autoTransfer = !autoTransfer;
            toolbarControl.SetTexture(GetIconTexture(autoTransfer), GetIconTexture(autoTransfer));
        }

        /// <summary>
        /// check if there is a scientist onboard so we can rerun things like goo or scijrs
        /// </summary>
        bool IsScientistOnBoard => CurrentVessel().GetVesselCrew().Any(k => k.trait == KerbalRoster.scientistTrait);

        /// <summary>
        /// just returns the correct icon name for the given state
        /// </summary>
        /// <param name="active"></param>
        /// <returns></returns>
        string GetIconTexture(bool active)
        {
            if (active) 
                return "ForScience/Icons/FS_active";
            else 
                return "ForScience/Icons/FS_inactive";
        }
    }
}
