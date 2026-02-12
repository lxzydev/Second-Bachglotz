using MSCLoader;
using UnityEngine;
using System.Linq;
using System.Text.RegularExpressions;
using HutongGames.PlayMaker;

namespace Second_Ferndale
{
	public class Second_Ferndale : Mod
    {
        public override string ID => "SecondBachglotz";
        public override string Name => "Second Bachglotz";
        public override string Author => "Roman266, ported lxzy";
        public override string Version => "1.0.0";
		public override string Description => "Adds a second Bachglotz to the game";
		public override Game SupportedGames => Game.MyWinterCar;

		private static Vector3 newpos;
		private static Quaternion newang;
		private static GameObject BACHCLONE;
		SettingsTextBox plateTextBox;

		public override void ModSetup()
		{
			SetupFunction(Setup.OnLoad, Mod_OnLoad);
			SetupFunction(Setup.OnSave, Mod_OnSave);
			SetupFunction(Setup.ModSettings, Mod_Settings);
		}

		private void Mod_Settings()
		{
			Settings.AddHeader("Second Bachglotz Settings");
			Settings.AddButton("Reset car", resetcar);
			Settings.AddText("Plate format: ABC-123 (2-3 letters, dash, 1-3 numbers)");
			plateTextBox = Settings.AddTextBox("plateString", "License Plate", "BHJ-49", "ABC-123");
			Settings.AddButton("Apply License Plate", ApplyPlate);
		}

		void SwapPlate(string text)
		{
			foreach (Transform t in BACHCLONE.GetComponentsInChildren<Transform>(true).Where(x => x.GetComponent<RegPlateGen>()))
			{
				RegPlateGen old = t.gameObject.GetComponent<RegPlateGen>();

				Texture2D blank = old.RegPlateBlank;
				Texture2D atlas = old.RegPlateAtlas;
				bool genStart = old.GenerateOnStart;
				bool filter = old.UseFilter;
				bool leadZero = old.AllowLeadingZero;
				bool adjChar = old.AdjustCharColor;
				float bChar = old.BrightnessChar;
				float cChar = old.ContrastChar;
				Color tChar = old.TintChar;
				bool adjBg = old.AdjustBgColor;
				float bBg = old.BrightnessBg;
				float cBg = old.ContrastBg;
				Color tBg = old.TintBg;
				bool save = old.SaveToFile;
				string fname = old.PlateFileName;

				Object.Destroy(old);
				RegPlateGen gen = t.gameObject.AddComponent<RegPlateGen>();

				gen.RegPlateBlank = blank;
				gen.RegPlateAtlas = atlas;
				gen.PlateString = text;
				gen.GenerateOnStart = genStart;
				gen.UseFilter = filter;
				gen.AllowLeadingZero = leadZero;
				gen.AdjustCharColor = adjChar;
				gen.BrightnessChar = bChar;
				gen.ContrastChar = cChar;
				gen.TintChar = tChar;
				gen.AdjustBgColor = adjBg;
				gen.BrightnessBg = bBg;
				gen.ContrastBg = cBg;
				gen.TintBg = tBg;
				gen.SaveToFile = save;
				gen.PlateFileName = fname;
			}
		}

		void ApplyPlate()
		{
			string text = plateTextBox.GetValue().ToUpper().Trim();
			if (!Regex.IsMatch(text, @"^[A-Z]{2,3}-\d{1,3}$"))
			{
				ModUI.ShowMessage("Bad plate format!\nUse 2-3 letters, dash, 1-3 numbers\nExample: BHJ-49", "Plate Error");
				return;
			}
			SwapPlate(text);
		}

        private void Mod_OnLoad()
        {
			newpos = new Vector3(-35.58809f, -0.5405251f, 40.06232f);
			newang = Quaternion.Euler(0f, 300f, 0f);
			BACHCLONE = Object.Instantiate(GameObject.Find("BACHGLOTZ(1905kg)"));
			BACHCLONE.name = "SECONDBACHGLOTZ(1905kg)";

			FsmVariables.GlobalVariables.FindFsmInt("PlayerKeyFerndale").Value = 1;

			foreach (PlayMakerFSM fsm in BACHCLONE.GetComponentsInChildren<PlayMakerFSM>().Where(f => f.FsmStates.Any(s => s.Name == "Load game" || s.Name == "Save game")))
				foreach (FsmState s in fsm.FsmStates.Where(s => s.Name == "Load game" || s.Name == "Save game"))
					s.Actions = new FsmStateAction[0];

			PlayMakerFSM fuelFSM = BACHCLONE.transform.Find("FuelTankBachglotz").GetComponent<PlayMakerFSM>();
			fuelFSM.FsmVariables.GetFsmString("UniqueTagFuelLevel").Value = "SecondBachglotzFuelLevel";

			SaveData data = SaveLoad.DeserializeSaveFile<SaveData>(this, "mySaveFile.save");
            if (data != null)
            {
				fuelFSM.FsmVariables.GetFsmFloat("FuelLevel").Value = data.save[0].fuel;
				BACHCLONE.transform.position = data.save[0].pos;
				BACHCLONE.transform.rotation = Quaternion.Euler(data.save[0].rotX, data.save[0].rotY, data.save[0].rotZ);
            }
            else
            {
				fuelFSM.FsmVariables.GetFsmFloat("FuelLevel").Value = 79;
				BACHCLONE.transform.position = newpos;
				BACHCLONE.transform.rotation = newang;
            }

			string plate = plateTextBox.GetValue().ToUpper().Trim();
			if (Regex.IsMatch(plate, @"^[A-Z]{2,3}-\d{1,3}$"))
				SwapPlate(plate);
        }

		private void Mod_OnSave()
		{
			SaveData sd = new SaveData();
			SaveDataList sdl = new SaveDataList();
			sdl.fuel = BACHCLONE.transform.Find("FuelTankBachglotz").GetComponent<PlayMakerFSM>().FsmVariables.GetFsmFloat("FuelLevel").Value;
			sdl.pos = BACHCLONE.transform.position;
			sdl.rotX = BACHCLONE.transform.rotation.eulerAngles.x;
			sdl.rotY = BACHCLONE.transform.rotation.eulerAngles.y;
			sdl.rotZ = BACHCLONE.transform.rotation.eulerAngles.z;
			sd.save.Add(sdl);
			SaveLoad.SerializeSaveFile(this, sd, "mySaveFile.save");
		}

		public static void resetcar()
		{
			BACHCLONE.transform.Find("FuelTankBachglotz").GetComponent<PlayMakerFSM>().FsmVariables.GetFsmFloat("FuelLevel").Value = 79;
            BACHCLONE.transform.position = newpos;
            BACHCLONE.transform.rotation = newang;
		}
    }
}
