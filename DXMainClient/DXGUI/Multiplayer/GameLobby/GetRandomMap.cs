using ClientCore;
using ClientGUI;
using DTAClient.Domain.Multiplayer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rampastring.Tools;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MapGenerator.Core;

namespace DTAClient.DXGUI.Multiplayer.GameLobby
{
    class GetRandomMap : XNAWindow
    {
        private const int OPTIONHEIGHT = 85;

        private XNALabel lblTitle;

        private XNALabel lblClimate; //气候
        private XNAClientDropDown ddClimate;

        private XNALabel lblPeople; //人数
        private XNAClientDropDown ddPeople;

        private XNAClientCheckBox cbDamage;//建筑物损伤

        private XNALabel lblSize;
        private XNAClientDropDown ddSize;
        private XNAClientButton btnGenerate;
        private XNAClientButton btnCancel;
        private XNAClientButton btnSave;
        private XNAButton btnpreview;

        private XNALabel lblStatus;

        private Thread thread1;

        private Thread thread;

        private bool Stop = false;

        private bool isSave;

        private string[] People;

        private string Damage = string.Empty;

        public MapLoader MapLoader;

        public GetRandomMap(WindowManager windowManager, MapLoader mapLoader) : base(windowManager)
        {
            MapLoader = mapLoader;
        }
        public override void Initialize()
        {
            base.Initialize();
            Name = "GetRandomMap";
            CenterOnParent();
#if WINFORMS
            System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = false;
#endif
            ClientRectangle = new Rectangle(200, 100, 800, 500);

            lblTitle = new XNALabel(WindowManager);
            lblTitle.ClientRectangle = new Rectangle(350, 20, 0, 0);
            lblTitle.CenterOnParentHorizontally();
            lblTitle.Text = "生成随机地图";

            lblStatus = new XNALabel(WindowManager);
            lblStatus.ClientRectangle = new Rectangle(360, 420, 0, 0);

            btnGenerate = new XNAClientButton(WindowManager);
            btnGenerate.Name = "btnGenerate";
            btnGenerate.ClientRectangle = new Rectangle(350, 460, 100, 20);
            btnGenerate.Text = "生成";
            btnGenerate.IdleTexture = AssetLoader.LoadTexture("92pxbtn.png");
            btnGenerate.HoverTexture = AssetLoader.LoadTexture("92pxbtn_c.png");
            btnGenerate.LeftClick += btnGenerat_LeftClick;


            btnCancel = new XNAClientButton(WindowManager);
            btnCancel.Name = "btnCancel";
            btnCancel.ClientRectangle = new Rectangle(40, 460, 100, 20);
            btnCancel.Text = "取消";
            btnCancel.IdleTexture = AssetLoader.LoadTexture("92pxbtn.png");
            btnCancel.HoverTexture = AssetLoader.LoadTexture("92pxbtn_c.png");
            btnCancel.LeftClick += btnCancel_LeftClick;

            btnSave = new XNAClientButton(WindowManager);
            btnSave.Name = "btnSave";
            btnSave.ClientRectangle = new Rectangle(660, 460, 100, 20);
            btnSave.Text = "保存";
            btnSave.IdleTexture = AssetLoader.LoadTexture("92pxbtn.png");
            btnSave.HoverTexture = AssetLoader.LoadTexture("92pxbtn_c.png");
            btnSave.Enabled = false;
            btnSave.LeftClick += btnSave_LeftClick;

            lblClimate = new XNALabel(WindowManager);
            lblClimate.ClientRectangle = new Rectangle(40, OPTIONHEIGHT, 0, 0);
            lblClimate.Text = "地形气候";

            ddClimate = new XNAClientDropDown(WindowManager);
            ddClimate.ClientRectangle = new Rectangle(lblClimate.X + 70, OPTIONHEIGHT, 80, 20);
            XNADropDownItem Desert = new XNADropDownItem();
            Desert.Text = "沙漠";
            Desert.Tag = "DESERT";
            XNADropDownItem Newurban = new XNADropDownItem();
            Newurban.Text = "城市";
            Newurban.Tag = "NEWURBAN";
            XNADropDownItem Temperate = new XNADropDownItem();
            Temperate.Text = "温和";
            Temperate.Tag = "TEMPERATE";
            XNADropDownItem Temperate_Islands = new XNADropDownItem();
            Temperate_Islands.Text = "岛屿";
            Temperate_Islands.Tag = "TEMPERATE_Islands";

            btnpreview = new XNAButton(WindowManager);
            btnpreview.ClientRectangle = new Rectangle(100, 150, 600, 250);


            ddClimate.AddItem("随机");
            ddClimate.AddItem(Temperate);
            ddClimate.AddItem(Temperate_Islands);
            ddClimate.AddItem(Newurban);
            ddClimate.AddItem(Desert);
            ddClimate.SelectedIndex = 0;

            lblPeople = new XNALabel(WindowManager);
            lblPeople.ClientRectangle = new Rectangle(ddClimate.X + 100, OPTIONHEIGHT, 80, 0);
            lblPeople.Text = "人数";

            ddPeople = new XNAClientDropDown(WindowManager);
            ddPeople.ClientRectangle = new Rectangle(lblPeople.X + 40, OPTIONHEIGHT, 80, 20);
            ddPeople.AddItem("随机");


            for (int i = 2; i <= 8; i++)
            {
                ddPeople.AddItem(i.ToString());
            }
            ddPeople.SelectedIndex = 0;

            lblSize = new XNALabel(WindowManager);
            lblSize.ClientRectangle = new Rectangle(ddPeople.X + 100, OPTIONHEIGHT, 0, 0);
            lblSize.Text = "大小";

            ddSize = new XNAClientDropDown(WindowManager);
            ddSize.ClientRectangle = new Rectangle(lblSize.X + 40, OPTIONHEIGHT, 80, 20);
            ddSize.AddItem("小");
            ddSize.AddItem("中等");
            ddSize.AddItem("大");
            ddSize.AddItem("超大");
            ddSize.SelectedIndex = 1;


            cbDamage = new XNAClientCheckBox(WindowManager);
            cbDamage.ClientRectangle = new Rectangle(ddSize.X + 150, OPTIONHEIGHT, 0, 0);
            cbDamage.Text = "建筑物随机损坏";


            //thread.Abort()
            AddChild(lblTitle);
            AddChild(lblStatus);
            AddChild(btnpreview);

            AddChild(lblClimate);
            AddChild(ddClimate);

            AddChild(lblPeople);
            AddChild(ddPeople);

            AddChild(lblSize);
            AddChild(ddSize);

            AddChild(cbDamage);
            AddChild(btnGenerate);
            AddChild(btnCancel);
            AddChild(btnSave);
        }

        public bool GetIsSave()
        {
            return isSave;
        }

        private void btnCancel_LeftClick(object sender, EventArgs e)
        {
            isSave = false;
            Disable();
        }

        private void btnSave_LeftClick(object sender, EventArgs e)
        {
            isSave = true;
            Disable();
        }

        private void btnGenerat_LeftClick(object sender, EventArgs e)
        {
            btnGenerate.Enabled = false;
            btnSave.Enabled = false;
            thread1 = new Thread(new ThreadStart(StartText));
            thread = new Thread(new ThreadStart(RunCmd));
            thread1.Start();
            thread.Start();
        }

        public void RunCmd()
        {
            Random r = new Random();

            string Generate = (string)ddClimate.SelectedItem.Tag;
            if (ddClimate.SelectedIndex == 0)
            {
                Generate = (string)ddClimate.Items[r.Next(1, 5)].Tag;
            }

            int sizex = 35 * (ddSize.SelectedIndex + 1) + r.Next(30, 50);
            int sizey = 35 * (ddSize.SelectedIndex + 1) + r.Next(30, 50);

            int playerCount;
            if (ddPeople.SelectedItem.Text == "随机")
                playerCount = r.Next(2, 8);
            else
                playerCount = int.Parse(ddPeople.SelectedItem.Text);

            bool randomBuildingDamage = cbDamage.Checked;

            string toolPath = Path.Combine(ProgramConstants.GamePath, "Resources", "RandomMapGenerator_RA2");

            RandomMapOptions options = new RandomMapOptions
            {
                Climate = Generate,
                PlayerCount = playerCount,
                Size = ddSize.SelectedIndex,
                RandomBuildingDamage = randomBuildingDamage
            };

            try
            {
                RandomMapGenerator generator = new RandomMapGenerator(toolPath, ProgramConstants.GamePath);
                generator.SetOutputPath(ProgramConstants.GamePath);
                generator.ProgressChanged += (s, e) => { /* optional progress handling */ };
                generator.GenerateRandomMap(options);
            }
            catch (Exception)
            {
                // ignore - StartText will check for preview existence
            }
            finally
            {
                Stop = true;
            }
        }
        public void StartText()
        {
            string[] TextList = {
                "正在驱散平民",
                "正在开采矿石",
                "正在装载基地建设车辆",
                "正在检查弹药",
                "正在为动员兵发放PPSh-41冲锋枪",
                "正在让幻影坦克熟悉环境",
                "正在安抚警犬",
                "正在捕捉海豚",
                "正在跟后勤争取资源",
                "正在给运输机补充燃料",
                "正在击沉潜艇",
                "正在给建筑粉刷" };
            Random r = new Random();
            while (!Stop)
            {
                lblStatus.Text = TextList[r.Next(TextList.Length)];
                Thread.Sleep(500);
            }

            try
            {
                string previewRelative = Path.Combine("Maps", "Custom", "随机地图.png");
                string previewFull = Path.Combine(ProgramConstants.GamePath, previewRelative);

                if (File.Exists(previewFull))
                {
                    btnpreview.IdleTexture = AssetLoader.LoadTextureUncached(previewRelative.Replace('\\', '/'));
                    lblStatus.Text = "完成";
                    btnGenerate.Enabled = true;
                    btnSave.Enabled = true;
                    Stop = false;
                    return;
                }
                else
                {
                    lblStatus.Text = "错误";
                    btnGenerate.Enabled = true;
                    Stop = false;
                    return;
                }
            }
            catch
            {
                lblStatus.Text = "错误";
                btnGenerate.Enabled = true;
                Stop = false;
                return;
            }


        }


        private string[] GetPeople(string Peoples)
        {
            int[] p = { 0, 0, 0, 0, 0, 0, 0, 0 };
            int Current;
            Random r = new Random();
            if (Peoples == "随机")
                Current = r.Next(2, 8);
            else
                Current = int.Parse(Peoples);

            while (Current > 0)
            {

                p[r.Next(8)]++;

                Current--;
            }
            return string.Join(",", p).Split(',');
        }


        public MapLoader GetMapLoader()
        {
            return MapLoader;
        }
    }
}
