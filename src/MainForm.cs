using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LabDisplaySwitch {
    internal sealed class MainForm:Form {
        private readonly NotifyIcon tray=new NotifyIcon();
        private readonly Label status=new Label(),screens=new Label(),detail=new Label();
        private readonly Button remote=new Button(),restore=new Button(),prepare=new Button(),save=new Button();
        private readonly CheckBox startup=new CheckBox(),compact=new CheckBox();
        private readonly System.Windows.Forms.Timer refresh=new System.Windows.Forms.Timer();
        private TrayHotkeys hotkeys;
        private readonly CursorRecovery cursorRecovery=new CursorRecovery();
        private bool busy,quitting,initialTray,restoreRequested;
        public MainForm(bool startInTray) {
            initialTray=startInTray;Text="Lab Display Switch";ClientSize=new Size(760,565);MinimumSize=new Size(740,600);StartPosition=FormStartPosition.CenterScreen;
            BackColor=Color.FromArgb(18,23,33);ForeColor=Color.FromArgb(235,240,249);Font=new Font("맑은 고딕",10);AutoScaleMode=AutoScaleMode.Dpi;
            var title=new Label { Text="Lab Display Switch",Font=new Font("맑은 고딕",23,FontStyle.Bold),AutoSize=false,Bounds=new Rectangle(28,24,700,49) };Controls.Add(title);
            var subtitle=new Label { Text="연구실의 작업을 집에서도 그대로",Bounds=new Rectangle(31,79,695,30),ForeColor=Color.FromArgb(163,177,200) };Controls.Add(subtitle);
            status.Bounds=new Rectangle(30,122,700,32);status.Font=new Font(Font,FontStyle.Bold);Controls.Add(status);
            screens.Bounds=new Rectangle(30,166,700,104);screens.BackColor=Color.FromArgb(29,37,52);screens.Padding=new Padding(14);Controls.Add(screens);
            Style(remote,"원격 모드 · 화면 숨기기",new Rectangle(30,290,340,48),Color.FromArgb(40,105,225));
            Style(restore,"연구실 모드로 복귀",new Rectangle(390,290,340,48),Color.FromArgb(48,64,87));
            Style(prepare,"가상 화면 준비",new Rectangle(30,354,165,39),Color.FromArgb(40,49,66));
            Style(save,"연구실 배치 저장",new Rectangle(211,354,206,39),Color.FromArgb(40,49,66));
            startup.Text="로그인할 때 트레이에서 시작";startup.Bounds=new Rectangle(433,357,300,34);Controls.Add(startup);
            using(var key=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))startup.Checked=key!=null&&key.GetValue("LabDisplaySwitch")!=null;
            compact.Text="가벼운 원격 화면 · 4K 화면을 QHD로 낮추기";compact.Bounds=new Rectangle(30,408,700,36);compact.Checked=(Store.Read<Settings>("settings.json")??new Settings()).Compact;Controls.Add(compact);
            compact.CheckedChanged+=delegate{if(!busy)UpdateStatus();};
            if((Store.Read<Settings>("settings.json")??new Settings()).RemoteLandscape)compact.Text="가로 원격 화면 · QHD 2개 / 100% (해제 시 4K 2개)";
            detail.Bounds=new Rectangle(30,466,700,80);detail.ForeColor=Color.FromArgb(163,177,200);detail.Text="출근 · 모니터 켜기: "+TrayHotkeys.RestoreShortcut+"\n퇴근 · 화면 숨기기: "+TrayHotkeys.HideShortcut+"\n창을 닫아 트레이에 둔 상태에서도 사용할 수 있습니다.";Controls.Add(detail);
            remote.Click+=delegate{bool alreadyRemote=Displays.Capture().Displays.All(d=>d.Virtual),choice=compact.Checked;Run("원격 화면을 설정하는 중…",delegate{if(alreadyRemote)Controller.UpdateRemoteQuality(choice);else Controller.EnterRemote(60,choice);},delegate{if(!alreadyRemote){var j=Store.Read<Journal>("transaction.json");using(var confirm=new ConfirmForm(j)){confirm.ShowDialog(this);}}UpdateStatus();});};
            restore.Click+=delegate{Run("연구실 화면을 복원하는 중…",Controller.RestoreLab,null);};
            save.Click+=delegate{Run("연구실 배치를 저장하는 중…",Displays.SaveLab,null);};
            prepare.Click+=delegate{Run("화면 설정을 처리하는 중…",delegate{
                if(Displays.Capture().Displays.All(d=>d.Virtual)){Controller.RepairRemoteLayout(true);return;}
                if(Store.Read<Profile>("lab-profile.json")==null)Displays.SaveLab();
                if(Installer.AlreadyInstalled()){Displays.RemoteProfile();return;}
                var process=Process.Start(new ProcessStartInfo(Store.Exe,"--install-driver --output \""+Store.PathFor("install-command.json")+"\"") { UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden });
                process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException("가상 화면 설치를 완료하지 못했습니다. 관리자 확인 또는 진단 기록을 확인하세요.");
            },null);};
            startup.CheckedChanged+=delegate{using(var key=Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")){if(startup.Checked)key.SetValue("LabDisplaySwitch","\""+Store.Exe+"\" --tray");else key.DeleteValue("LabDisplaySwitch",false);}};
            var menu=new ContextMenuStrip();menu.Items.Add("열기",null,delegate{Show();WindowState=FormWindowState.Normal;Activate();});
            menu.Items.Add("연구실 모드로 복귀",null,delegate{Run("연구실 화면을 복원하는 중…",Controller.RestoreLab,null);});
            menu.Items.Add("화면 숨기기 · "+TrayHotkeys.HideShortcut,null,delegate{SwitchFromHotkey(true);});
            menu.Items.Add("진단 폴더",null,delegate{Process.Start(Store.Root);});
            menu.Items.Add("트레이 앱 종료",null,delegate{if(busy)return;quitting=true;Close();});
            Icon=Icon.ExtractAssociatedIcon(Store.Exe);tray.Icon=Icon;tray.Text="Lab Display Switch";tray.ContextMenuStrip=menu;tray.Visible=true;tray.DoubleClick+=delegate{Show();WindowState=FormWindowState.Normal;Activate();};
            refresh.Interval=5000;refresh.Tick+=delegate{if(!busy)UpdateStatus();};refresh.Start();
            Shown+=delegate{try{Controller.ReconcileStartup();Controller.EnsureGuard();hotkeys=new TrayHotkeys(SwitchFromHotkey);if(!hotkeys.RestoreRegistered||!hotkeys.HideRegistered)detail.Text="복귀·숨기기 단축키를 등록하지 못했습니다. 다른 앱에서 Ctrl + Alt + Home / End를 사용하는지 확인해 주세요.";UpdateStatus();}catch(Exception e){detail.Text=e.Message;}if(initialTray){initialTray=false;Hide();}};
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(e.CloseReason==CloseReason.UserClosing&&!quitting){e.Cancel=true;Hide();}};
            FormClosed+=delegate{if(hotkeys!=null)hotkeys.Dispose();tray.Visible=false;tray.Dispose();refresh.Dispose();};
        }
        private void SwitchFromHotkey(bool hide) {
            Store.Log("Tray shortcut received: "+(hide?"hide":"restore"));
            if(busy){if(!hide)restoreRequested=true;return;}
            if(hide) {
                Run("연구실 화면을 숨기는 중…",delegate{if(hotkeys==null||!hotkeys.RestoreRegistered)throw new InvalidOperationException("복귀 단축키가 등록되지 않아 화면을 숨기지 않았습니다.");Controller.HideFromHotkey();},null);
            }else Run("연구실 화면을 복원하는 중…",Controller.RestoreLab,null);
        }
        private void Style(Button button,string text,Rectangle bounds,Color color) { button.Text=text;button.Bounds=bounds;button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=0;button.BackColor=color;button.ForeColor=ForeColor;button.Cursor=Cursors.Hand;Controls.Add(button); }
        private void Run(string message,Action action,Action completed) {
            if(busy)return;busy=true;status.Text=message;remote.Enabled=restore.Enabled=prepare.Enabled=save.Enabled=false;
            new Thread(delegate(){Exception failure=null;try{Native.SetThreadDpiAwarenessContext(new IntPtr(-4));action();}catch(Exception e){failure=e;Store.Log(e.ToString());}
                if(!IsDisposed)BeginInvoke((Action)delegate{busy=false;remote.Enabled=restore.Enabled=prepare.Enabled=save.Enabled=true;UpdateStatus();if(restoreRequested){restoreRequested=false;SwitchFromHotkey(false);return;}if(failure!=null){detail.Text=failure.Message;MessageBox.Show(this,failure.Message,"Lab Display Switch",MessageBoxButtons.OK,MessageBoxIcon.Warning);}else if(completed!=null)completed();});
            }){IsBackground=true}.Start();
        }
        private void UpdateStatus() {
            try {
                var p=Displays.Capture();bool isRemote=p.Displays.Count>0&&p.Displays.All(d=>d.Virtual);
                status.Text=isRemote?"원격 모드 · 실제 모니터 출력 숨김":"연구실 모드 · 실제 모니터 사용 중";
                status.ForeColor=isRemote?Color.FromArgb(116,218,171):ForeColor;
                screens.Text=String.Join("\n",p.Displays.Select(d=>d.Name+"   "+d.Width+" × "+d.Height+"   "+d.Scale+"%"));
                var lab=Store.Read<Profile>("lab-profile.json");var settings=Store.Read<Settings>("settings.json")??new Settings();remote.Enabled=lab!=null&&(!isRemote||compact.Checked!=settings.Compact);save.Enabled=!isRemote;restore.Enabled=lab!=null;
                remote.Text=isRemote?"원격 화면 설정 적용":"원격 모드 · 화면 숨기기";prepare.Text=isRemote?"창 배치 복구":"가상 화면 준비";
                var journal=Store.Read<Journal>("transaction.json");
                cursorRecovery.Poll(isRemote&&journal!=null&&journal.Phase=="remote"&&journal.Confirmed);
                if(isRemote&&journal!=null&&journal.After!=null){var mismatches=p.Displays.Where(d=>journal.After.Displays.Any(a=>a.Key==d.Key&&a.Scale!=d.Scale)).ToList();if(mismatches.Count>0)detail.Text="가상 화면 배율 확인 필요: "+String.Join(" / ",journal.After.Displays.Select(d=>d.Scale+"%"))+"\n화면 복귀: "+TrayHotkeys.RestoreShortcut;}
            }catch(Exception e){status.Text="화면 상태 확인 필요";detail.Text=e.Message;}
        }
    }
    internal sealed class ConfirmForm:Form {
        private readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        private readonly Label time=new Label();private readonly Journal journal;
        public ConfirmForm(Journal j) {
            journal=j;Text="원격 화면 확인";ClientSize=new Size(530,245);StartPosition=FormStartPosition.CenterScreen;TopMost=true;Font=new Font("맑은 고딕",10);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
            Controls.Add(new Label { Text="다른 기기의 Chrome 원격 데스크톱에서\n두 화면과 입력이 정상인지 확인한 뒤 유지해 주세요.",Bounds=new Rectangle(24,25,485,65) });
            time.Bounds=new Rectangle(24,100,485,35);Controls.Add(time);
            var keep=new Button { Text="확인했음 · 원격 모드 유지",Bounds=new Rectangle(24,165,290,43) };Controls.Add(keep);
            var back=new Button { Text="화면 복귀",Bounds=new Rectangle(330,165,177,43) };Controls.Add(back);
            keep.Click+=delegate{try{Controller.Confirm(journal.Id);Close();}catch(Exception e){MessageBox.Show(e.Message);Close();}};
            back.Click+=delegate{Controller.RestoreLab();Close();};
            timer.Interval=250;timer.Tick+=delegate{int left=(int)Math.Ceiling((DateTime.Parse(journal.DeadlineUtc,null,System.Globalization.DateTimeStyles.RoundtripKind)-DateTime.UtcNow).TotalSeconds);time.Text=left+"초 후 자동으로 원래 화면으로 돌아갑니다.";if(left<=0)Close();};timer.Start();
            FormClosed+=delegate{timer.Dispose();};
        }
    }
}
