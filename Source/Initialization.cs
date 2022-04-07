
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using KSP_Log;
using ToolbarControl_NS;


namespace ForScience
{

    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class InitLog : MonoBehaviour
    {
        public static KSP_Log.Log Log;

        public static void SetLogLevel(int i)
        {
            Log.SetLevel((Log.LEVEL)i);
        }

        protected void Awake()
        {
            Log = new KSP_Log.Log("ForScience"
#if DEBUG
                , KSP_Log.Log.LEVEL.INFO
#endif
                );
        }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        void Start()
        {
            ToolbarControl.RegisterMod(ForScience.MODID, ForScience.MODNAME);
        }
    }

    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class Statics : MonoBehaviour
    {
        bool initted = false;

        void Start()
        {
        }


        void OnGUI()
        {
            if (!initted)
            {
                initted = true;
            }
        }
    }

}
