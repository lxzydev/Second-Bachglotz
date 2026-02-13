using MSCLoader;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HutongGames.PlayMaker;
using System.Collections;

namespace Second_Ferndale
{
    public class Second_Ferndale : Mod
    {
        public override string ID => "SecondBachglotz";
        public override string Name => "Second Bachglotz";
        public override string Author => "Roman266, ported lxzy";
        public override string Version => "1.0.2";
        public override string Description => "Adds a second Bachglotz to the game";
        public override Game SupportedGames => Game.MyWinterCar;

        private static Vector3 newpos;
        private static Quaternion newang;
        private static GameObject BACHCLONE;
        SettingsTextBox plateTextBox;

        private Material[] paintMaterials;
        private Texture[] originalTextures;
        private Texture[] originalSpecTextures;
        private Texture2D blankTex;
        private float paintR = -1f, paintG = -1f, paintB = -1f;
        private bool paintGUIOpen;
        private bool useDefaultLivery;
        private SettingsKeybind paintKey;
        private PlayMakerFSM updateCursorFSM;
        private PlayMakerFSM cameraSettingsFSM;
        private bool wasCursorFSMEnabled;
        private bool wasCameraFSMEnabled;
        private PaintBehaviour paintBehaviour;
        private SettingsCheckBox tachToggle;
        private SettingsSliderInt frontGrip;
        private SettingsSliderInt rearGrip;
        private SettingsDropDownList transmissionSelect;

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, Mod_OnLoad);
            SetupFunction(Setup.OnSave, Mod_OnSave);
            SetupFunction(Setup.ModSettings, Mod_Settings);
            SetupFunction(Setup.Update, Mod_Update);
            SetupFunction(Setup.OnGUI, Mod_OnGUI);
        }

        private void Mod_Settings()
        {
            Settings.AddHeader("Second Bachglotz Settings");
            Settings.AddButton("Reset car", resetcar);
            Settings.AddText("Plate format: ABC-123 (2-3 letters, dash, 1-3 numbers)");
            plateTextBox = Settings.AddTextBox("plateString", "License Plate", "BHJ-49", "ABC-123");
            Settings.AddButton("Apply License Plate", ApplyPlate);
            Settings.AddHeader("Dashboard");
            tachToggle = Settings.AddCheckBox("tachToggle", "Enable Tachometer", true, TachChanged);
            Settings.AddHeader("Drivetrain");
            transmissionSelect = Settings.AddDropDownList("transmissionSelect", "Transmission", new string[] { "RWD", "FWD", "AWD" }, 0, ApplyTransmission);
            Settings.AddHeader("Axle Grip");
            frontGrip = Settings.AddSlider("frontGrip", "Front Axle Grip", 1, 5, 1, ApplyGrip);
            rearGrip = Settings.AddSlider("rearGrip", "Rear Axle Grip", 1, 5, 1, ApplyGrip);
            Settings.AddHeader("Livery & Paint");
            Settings.AddText("Custom textures go in the mod Assets folder: bachglotz_paint.png, bachglotz_paint_s.png");
            Settings.AddButton("Reload Textures", ReloadTextures);
            paintKey = Keybind.Add("paintGUI", "Paint GUI", KeyCode.P, KeyCode.LeftControl);
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

            RegisterCloneHeatSource();

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

            paintMaterials = new Material[]
            {
                BACHCLONE.transform.Find("MESH/body").GetComponent<MeshRenderer>().material,
                BACHCLONE.transform.Find("DriverDoors/door(leftx)/doors").GetComponent<MeshRenderer>().material,
                BACHCLONE.transform.Find("DriverDoors/door(right)/doors").GetComponent<MeshRenderer>().material,
                BACHCLONE.transform.Find("Bootlid/Bootlid/bootlid").GetComponent<MeshRenderer>().material
            };

            originalTextures = new Texture[paintMaterials.Length];
            originalSpecTextures = new Texture[paintMaterials.Length];
            for (int i = 0; i < paintMaterials.Length; i++)
            {
                originalTextures[i] = paintMaterials[i].mainTexture;
                originalSpecTextures[i] = paintMaterials[i].GetTexture("_SpecGlossMap");
            }

            blankTex = new Texture2D(1, 1);
            blankTex.SetPixel(0, 0, Color.white);
            blankTex.Apply();

            ApplyBlankBase();
            LoadTextures();
            LoadSavedPaint();

            if (paintR < 0f)
            {
                if (data != null && data.save[0].paintR >= 0f)
                {
                    paintR = data.save[0].paintR;
                    paintG = data.save[0].paintG;
                    paintB = data.save[0].paintB;
                    Color c = new Color(paintR, paintG, paintB, 1f);
                    for (int i = 0; i < paintMaterials.Length; i++)
                        paintMaterials[i].color = c;
                }
            }
            else
            {
                Color c = new Color(paintR, paintG, paintB, 1f);
                for (int i = 0; i < paintMaterials.Length; i++)
                    paintMaterials[i].color = c;
            }

            BACHCLONE.transform.Find("LOD/Dashboard/tach3").gameObject.SetActive(tachToggle.GetValue());

            ApplyGrip();
            ApplyTransmission();

            GameObject bhv = new GameObject("SecondBachglotzPaintBehaviour");
            paintBehaviour = bhv.AddComponent<PaintBehaviour>();
            paintBehaviour.mod = this;
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
            sdl.paintR = paintR;
            sdl.paintG = paintG;
            sdl.paintB = paintB;
            sd.save.Add(sdl);
            SaveLoad.SerializeSaveFile(this, sd, "mySaveFile.save");
        }

        public static void resetcar()
        {
            BACHCLONE.transform.Find("FuelTankBachglotz").GetComponent<PlayMakerFSM>().FsmVariables.GetFsmFloat("FuelLevel").Value = 79;
            BACHCLONE.transform.position = newpos;
            BACHCLONE.transform.rotation = newang;
        }

        private void RegisterCloneHeatSource()
        {
            Transform hs = BACHCLONE.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name.Contains("HeatSource"));
            if (hs == null) return;

            GameObject bodyTemp = GameObject.Find("PLAYER/BodyTemp");
            if (bodyTemp == null) return;

            PlayMakerArrayListProxy list = bodyTemp.GetComponents<PlayMakerArrayListProxy>()
                .FirstOrDefault(p => p.referenceName == "HeatSources");
            if (list == null) return;

            list.Add(hs.gameObject, "GameObject");
        }

        private void ApplyGrip()
        {
            Axles axles = BACHCLONE.GetComponent<Axles>();
            axles.frontAxle.forwardGripFactor = frontGrip.GetValue();
            axles.frontAxle.sidewaysGripFactor = frontGrip.GetValue();
            axles.rearAxle.forwardGripFactor = rearGrip.GetValue();
            axles.rearAxle.sidewaysGripFactor = rearGrip.GetValue();
        }

        private void ApplyTransmission()
        {
            Drivetrain dt = BACHCLONE.GetComponent<Drivetrain>();
            int sel = transmissionSelect.GetSelectedItemIndex();
            if (sel == 0) { dt.transmission = Drivetrain.Transmissions.RWD; dt.SetTransmission(Drivetrain.Transmissions.RWD); }
            else if (sel == 1) { dt.transmission = Drivetrain.Transmissions.FWD; dt.SetTransmission(Drivetrain.Transmissions.FWD); }
            else if (sel == 2) { dt.transmission = Drivetrain.Transmissions.AWD; dt.SetTransmission(Drivetrain.Transmissions.AWD); }
        }

        private void TachChanged()
        {
            BACHCLONE.transform.Find("LOD/Dashboard/tach3").gameObject.SetActive(tachToggle.GetValue());
        }

        private void ApplyBlankBase()
        {
            for (int i = 0; i < paintMaterials.Length; i++)
            {
                paintMaterials[i].mainTexture = blankTex;
                paintMaterials[i].SetTexture("_SpecGlossMap", blankTex);
            }
            useDefaultLivery = false;
        }

        private void LoadTextures()
        {
            string assetsFolder = ModLoader.GetModAssetsFolder(this);
            string paintPath = Path.Combine(assetsFolder, "bachglotz_paint.png");
            if (File.Exists(paintPath))
            {
                byte[] paintData = File.ReadAllBytes(paintPath);
                Texture2D paintTex = new Texture2D(2, 2);
                paintTex.LoadImage(paintData);
                for (int i = 0; i < paintMaterials.Length; i++)
                    paintMaterials[i].mainTexture = paintTex;
            }
            string specPath = Path.Combine(assetsFolder, "bachglotz_paint_s.png");
            if (File.Exists(specPath))
            {
                byte[] specData = File.ReadAllBytes(specPath);
                Texture2D specTex = new Texture2D(2, 2);
                specTex.LoadImage(specData);
                for (int i = 0; i < paintMaterials.Length; i++)
                    paintMaterials[i].SetTexture("_SpecGlossMap", specTex);
            }
        }

        private void ReloadTextures()
        {
            if (paintMaterials == null)
                return;
            ApplyBlankBase();
            LoadTextures();
            if (paintR >= 0f)
            {
                Color c = new Color(paintR, paintG, paintB, 1f);
                for (int i = 0; i < paintMaterials.Length; i++)
                    paintMaterials[i].color = c;
            }
        }

        private void SavePaint()
        {
            string path = Path.Combine(ModLoader.GetModAssetsFolder(this), "paint_color.txt");
            File.WriteAllText(path, paintR + "\n" + paintG + "\n" + paintB);
        }

        private void LoadSavedPaint()
        {
            string path = Path.Combine(ModLoader.GetModAssetsFolder(this), "paint_color.txt");
            if (!File.Exists(path))
                return;
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 3)
                return;
            float r, g, b;
            if (float.TryParse(lines[0], out r) && float.TryParse(lines[1], out g) && float.TryParse(lines[2], out b))
            {
                paintR = r;
                paintG = g;
                paintB = b;
            }
        }

        private void FindCursorControlFSMs()
        {
            GameObject player = GameObject.Find("PLAYER");
            if (player != null)
                updateCursorFSM = player.GetComponentsInChildren<PlayMakerFSM>(true).FirstOrDefault(f => f.FsmName == "Update Cursor");
            GameObject cam = GameObject.Find("FPSCamera");
            if (cam != null)
                cameraSettingsFSM = cam.GetComponentsInChildren<PlayMakerFSM>(true).FirstOrDefault(f => f.FsmName == "CameraSettings");
        }

        private void OpenPaintMenu()
        {
            if (updateCursorFSM == null || cameraSettingsFSM == null)
                FindCursorControlFSMs();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (updateCursorFSM != null)
            {
                wasCursorFSMEnabled = updateCursorFSM.enabled;
                updateCursorFSM.enabled = false;
            }
            if (cameraSettingsFSM != null)
            {
                wasCameraFSMEnabled = cameraSettingsFSM.enabled;
                cameraSettingsFSM.enabled = false;
            }
        }

        private void ClosePaintMenu()
        {
            if (updateCursorFSM != null && wasCursorFSMEnabled)
                updateCursorFSM.enabled = true;
            if (cameraSettingsFSM != null && wasCameraFSMEnabled)
                cameraSettingsFSM.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Mod_Update()
        {
            if (paintKey.GetKeybindDown())
            {
                paintGUIOpen = !paintGUIOpen;
                if (paintGUIOpen)
                    OpenPaintMenu();
                else
                    ClosePaintMenu();
            }
            if (paintGUIOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                paintGUIOpen = false;
                ClosePaintMenu();
            }
        }

        public void PaintLateUpdate()
        {
            if (paintGUIOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }

        private void Mod_OnGUI()
        {
            if (!paintGUIOpen)
                return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            GUI.ModalWindow(9401, new Rect((Screen.width - 530) / 2, 10f, 530f, 300f), PaintWindow, "Second Bachglotz Paint");
        }

        private void PaintWindow(int windowID)
        {
            GUI.Label(new Rect(10, 25, 510, 20), string.Format("R: {0:F3}  G: {1:F3}  B: {2:F3}", paintR < 0 ? 1f : paintR, paintG < 0 ? 1f : paintG, paintB < 0 ? 1f : paintB));

            float y = 50;
            GUI.Label(new Rect(10, y, 30, 25), "R:");
            if (GUI.Button(new Rect(40, y, 40, 25), "---")) { if (paintR < 0) paintR = 1f; paintR = Mathf.Max(0f, paintR - 0.1f); }
            if (GUI.Button(new Rect(85, y, 35, 25), "--")) { if (paintR < 0) paintR = 1f; paintR = Mathf.Max(0f, paintR - 0.01f); }
            if (GUI.Button(new Rect(125, y, 30, 25), "-")) { if (paintR < 0) paintR = 1f; paintR = Mathf.Max(0f, paintR - 0.001f); }
            if (GUI.Button(new Rect(160, y, 30, 25), "+")) { if (paintR < 0) paintR = 1f; paintR = Mathf.Min(1f, paintR + 0.001f); }
            if (GUI.Button(new Rect(195, y, 35, 25), "++")) { if (paintR < 0) paintR = 1f; paintR = Mathf.Min(1f, paintR + 0.01f); }
            if (GUI.Button(new Rect(235, y, 40, 25), "+++")) { if (paintR < 0) paintR = 1f; paintR = Mathf.Min(1f, paintR + 0.1f); }

            y = 80;
            GUI.Label(new Rect(10, y, 30, 25), "G:");
            if (GUI.Button(new Rect(40, y, 40, 25), "---")) { if (paintG < 0) paintG = 1f; paintG = Mathf.Max(0f, paintG - 0.1f); }
            if (GUI.Button(new Rect(85, y, 35, 25), "--")) { if (paintG < 0) paintG = 1f; paintG = Mathf.Max(0f, paintG - 0.01f); }
            if (GUI.Button(new Rect(125, y, 30, 25), "-")) { if (paintG < 0) paintG = 1f; paintG = Mathf.Max(0f, paintG - 0.001f); }
            if (GUI.Button(new Rect(160, y, 30, 25), "+")) { if (paintG < 0) paintG = 1f; paintG = Mathf.Min(1f, paintG + 0.001f); }
            if (GUI.Button(new Rect(195, y, 35, 25), "++")) { if (paintG < 0) paintG = 1f; paintG = Mathf.Min(1f, paintG + 0.01f); }
            if (GUI.Button(new Rect(235, y, 40, 25), "+++")) { if (paintG < 0) paintG = 1f; paintG = Mathf.Min(1f, paintG + 0.1f); }

            y = 110;
            GUI.Label(new Rect(10, y, 30, 25), "B:");
            if (GUI.Button(new Rect(40, y, 40, 25), "---")) { if (paintB < 0) paintB = 1f; paintB = Mathf.Max(0f, paintB - 0.1f); }
            if (GUI.Button(new Rect(85, y, 35, 25), "--")) { if (paintB < 0) paintB = 1f; paintB = Mathf.Max(0f, paintB - 0.01f); }
            if (GUI.Button(new Rect(125, y, 30, 25), "-")) { if (paintB < 0) paintB = 1f; paintB = Mathf.Max(0f, paintB - 0.001f); }
            if (GUI.Button(new Rect(160, y, 30, 25), "+")) { if (paintB < 0) paintB = 1f; paintB = Mathf.Min(1f, paintB + 0.001f); }
            if (GUI.Button(new Rect(195, y, 35, 25), "++")) { if (paintB < 0) paintB = 1f; paintB = Mathf.Min(1f, paintB + 0.01f); }
            if (GUI.Button(new Rect(235, y, 40, 25), "+++")) { if (paintB < 0) paintB = 1f; paintB = Mathf.Min(1f, paintB + 0.1f); }

            Texture2D preview = new Texture2D(1, 1);
            preview.SetPixel(0, 0, new Color(paintR < 0 ? 1f : paintR, paintG < 0 ? 1f : paintG, paintB < 0 ? 1f : paintB, 1f));
            preview.Apply();
            GUI.DrawTexture(new Rect(300, 50, 80, 85), preview);

            if (GUI.Button(new Rect(400, 50, 110, 30), "Apply Paint"))
            {
                if (useDefaultLivery)
                {
                    ApplyBlankBase();
                    LoadTextures();
                }
                Color c = new Color(paintR < 0 ? 1f : paintR, paintG < 0 ? 1f : paintG, paintB < 0 ? 1f : paintB, 1f);
                for (int i = 0; i < paintMaterials.Length; i++)
                    paintMaterials[i].color = c;
            }

            if (GUI.Button(new Rect(400, 90, 110, 30), "Reset to White"))
            {
                if (useDefaultLivery)
                {
                    ApplyBlankBase();
                    LoadTextures();
                }
                paintR = 1f; paintG = 1f; paintB = 1f;
                for (int i = 0; i < paintMaterials.Length; i++)
                    paintMaterials[i].color = Color.white;
            }

            if (GUI.Button(new Rect(400, 130, 110, 30), "Save Paint"))
                SavePaint();

            if (GUI.Button(new Rect(10, 145, 150, 30), "Reload Textures"))
                ReloadTextures();

            if (GUI.Button(new Rect(400, 170, 110, 30), "Close"))
            {
                paintGUIOpen = false;
                ClosePaintMenu();
            }

            if (GUI.Button(new Rect(10, 185, 200, 30), "Reset Paint (Original)"))
            {
                if (useDefaultLivery)
                {
                    ApplyBlankBase();
                    LoadTextures();
                }
                paintR = -1f; paintG = -1f; paintB = -1f;
                for (int i = 0; i < paintMaterials.Length; i++)
                    paintMaterials[i].color = Color.white;
            }

            if (GUI.Button(new Rect(10, 225, 200, 30), "Restore Default Livery"))
            {
                for (int i = 0; i < paintMaterials.Length; i++)
                {
                    paintMaterials[i].mainTexture = originalTextures[i];
                    paintMaterials[i].SetTexture("_SpecGlossMap", originalSpecTextures[i]);
                    paintMaterials[i].color = Color.white;
                }
                paintR = -1f; paintG = -1f; paintB = -1f;
                useDefaultLivery = true;
            }

            GUI.DragWindow();
        }

        public class PaintBehaviour : MonoBehaviour
        {
            public Second_Ferndale mod;
            void LateUpdate() { mod.PaintLateUpdate(); }
        }
    }
}
