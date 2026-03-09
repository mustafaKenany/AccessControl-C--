using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using FCardCDrive;
using FCardCDrive.Connect;
using System.IO.Ports;
using FCardCDrive.FC8900;
using System.Net;
using FCardCDrive.Connect.TCPServer;
using DataTool;
using System.Diagnostics;
namespace demo
{
    public partial class CSelectDoor : Form
    {
        public CSelectDoor()
        {
            InitializeComponent();
        }

        public string ip;
        public string trgip;
        public string mac;
        public string sn;
        public string password;
        public string gateway;
        public int doorCount;
        public string tcpport;
        public string udpport;
        public string mode;
        public int monitor, accessCount;
        //Define the global command operation object
        public FCardCDrive.ConnectMain mIO = null;
        private void CSelectDoor_Load(object sender, EventArgs e)
        {
            //Initialize command operation object
            if (mIO == null)
            {
                mIO = new FCardCDrive.ConnectMain();
                mIO.CommandAchieve += _CommandAchieve;//Command success event
                mIO.CommandTimeout += _CommandTimeout;//Command timeout event
                mIO.ConnectError += _ConnectError;//Connection error event
                mIO.PasswordError += _PasswordError;//Password error event
            }
        }
        private void _CommandAchieve(ConnectInfo oInfo, int iCommandCode, object oValue)
        {
            if (iCommandCode == (int)FCardCDrive.ConnectMain.eCommandCode.cmdOpenRelay)
            {
                //通过iCommandCode判定属于哪个命令的事件返回
            }
        }
        private void _CommandTimeout(ConnectInfo oInfo, int iCommandCode, int iStep, object oValue)
        {
            //命令time out
        }

        /// <summary>
        /// 连接错误
        /// </summary>
        /// <param name="oInfo">原封不动返回执行命令时候的连接参数对象</param>
        /// <param name="iCommandCode">命令代码，与枚举对象一一对应</param>
        /// <param name="oValue">命令返回值</param>
        private void _ConnectError(ConnectInfo oInfo, int iCommandCode, object oValue)
        {
            MessageBox.Show("连接失败");
            //throw new NotImplementedException();
        }

        /// <summary>
        /// 密码错误
        /// </summary>
        /// <param name="oInfo">原封不动返回执行命令时候的连接参数对象</param>
        /// <param name="iCommandCode">命令代码，与枚举对象一一对应</param>
        /// <param name="oValue">命令返回值</param>
        private void _PasswordError(ConnectInfo oInfo, int iCommandCode, object oValue)
        {

        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (!checkBox1.Checked && !checkBox2.Checked && !checkBox3.Checked && !checkBox4.Checked)
            {
                MessageBox.Show("请choose需要Remote Open door的设备");
            }
            else
            {
                bool[] doors = new bool[4];
                if (checkBox1.Checked)
                {
                    doors[0] = true;
                }
                else
                {
                    doors[0] = false;
                }
                if (checkBox2.Checked)
                {
                    doors[1] = true;
                }
                else
                {
                    doors[1] = false;
                }
                if (checkBox3.Checked)
                {
                    doors[2] = true;
                }
                else
                {
                    doors[2] = false;
                }
                if (checkBox4.Checked)
                {
                    doors[3] = true;
                }
                else
                {
                    doors[3] = false;
                }

                //IntPtr result = cl2.remoteOpen(sn, ip, Convert.ToInt16(tcpport), password, doorCheck, accessCount, 0);

                mIO.Command(GetConnectInfo(), "OpenRelay", doors);//Open door command
                //mIO.Command(GetConnectInfo(), "BeginWatch");
            }
        }
        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        //Create connection parameter object
        private FCardCDrive.Connect.ConnectInfo GetConnectInfo()
        {
            FCardCDrive.Connect.ConnectInfo oInfo = new FCardCDrive.Connect.ConnectInfo();
            oInfo.SN = sn;// 16 characters
            oInfo.IP = ip;//IP地址
            oInfo.NetPort = 8000;//TCP default port is 8000
            oInfo.Password = password;//Communication password 8 hexadecimal characters
            oInfo.EquptType = FCardCDrive.Connect.ConnectInfo.e_EquptType.FC8900;//Specify device type
            oInfo.ConnType = FCardCDrive.Connect.ConnectInfo.e_ConnectType.OnTCPClient;//Specify connection type
            oInfo.RestartCount = 3;//number of retries
            oInfo.TimeOutMSEL = 6000;//time out
            return oInfo;
        }
        private void CSelectDoor_FormClosing(object sender, FormClosingEventArgs e)
        {
            
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (!checkBox1.Checked && !checkBox2.Checked && !checkBox3.Checked && !checkBox4.Checked)
            {
                MessageBox.Show("请choose需要Remote Open door的设备");
            }
            else
            {
                bool[] doors = new bool[4];
                if (checkBox1.Checked)
                {
                    doors[0] = true;
                }
                else
                {
                    doors[0] = false;
                }
                if (checkBox2.Checked)
                {
                    doors[1] = true;
                }
                else
                {
                    doors[1] = false;
                }
                if (checkBox3.Checked)
                {
                    doors[2] = true;
                }
                else
                {
                    doors[2] = false;
                }
                if (checkBox4.Checked)
                {
                    doors[3] = true;
                }
                else
                {
                    doors[3] = false;
                }

                //IntPtr result = cl2.remoteOpen(sn, ip, Convert.ToInt16(tcpport), password, doorCheck, accessCount, 0);

                mIO.Command(GetConnectInfo(), "CloseRelay", doors);//Open door command
                //mIO.Command(GetConnectInfo(), "BeginWatch");
            }
        }
    }
}
