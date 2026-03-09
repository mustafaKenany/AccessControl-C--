using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Threading;
using System.Activities.Statements;
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
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[0].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[6].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[2].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                string FormTitle = this.Text;

                ModifyCommPassword mPwd = new ModifyCommPassword();
                mPwd.temp = s;
                mPwd.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }

        }

        private void button8_Click(object sender, EventArgs e)
        {
            //Initialize the network环境
            IntPtr w2 = cl2.clearNet();
            //Release all memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                cl2.SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);
            }
            w2 = cl2.initNet("C#");
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string FormTitle = this.Text;

                ModifyIP mIP = new ModifyIP();
                mIP.password = password;
                mIP.sn = sn;
                mIP.ip = ip;
                mIP.gateway = gateway;
                mIP.mode = mode;
                mIP.textBox1.Text = mac;
                mIP.textBox3.Text = ip;
                mIP.textBox2.Text = "255.255.255.0";
                mIP.textBox4.Text = gateway;
                mIP.comboBox1.Text = mode;
                mIP.doorCount = doorCount;
                mIP.textBox7.Text = tcpport;
                mIP.textBox8.Text = udpport;
                mIP.ShowDialog();
                if (mIP.a==1)
                {
                    dataGridView1.Rows[index].Cells[1].Value = mIP.textBox3.Text;
                }
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;

        }

        private void button7_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string FormTitle = this.Text;
                CDelayTime delay = new CDelayTime();
                delay.ip = ip;
                delay.mac = mac;
                delay.sn = sn;
                delay.password = password;
                delay.gateway = gateway;
                delay.doorCount = doorCount;
                delay.tcpport = tcpport;
                delay.udpport = udpport;
                delay.mode = mode;
                if (doorCount==1)
                {
                    delay.checkBox1.Enabled = true;
                    delay.checkBox2.Enabled = false;
                    delay.checkBox3.Enabled = false;
                    delay.checkBox4.Enabled = false;
                }
                else if (doorCount == 2)
                {
                    delay.checkBox1.Enabled = true;
                    delay.checkBox2.Enabled = true;
                    delay.checkBox3.Enabled = false;
                    delay.checkBox4.Enabled = false;
                }
                else
                {
                    delay.checkBox1.Enabled = true;
                    delay.checkBox2.Enabled = true;
                    delay.checkBox3.Enabled = true;
                    delay.checkBox4.Enabled = true;
                }
                delay.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string FormTitle = this.Text;
                CSelectDoor remote = new CSelectDoor();
                remote.mIO = demo.DataMonitor.mIO;
                remote.ip = ip;
                remote.mac = mac;
                remote.sn = sn;
                remote.password = password;
                remote.gateway = gateway;
                remote.doorCount = doorCount;
                remote.tcpport = tcpport;
                remote.udpport = udpport;
                remote.mode = mode;
                remote.monitor = 0;
                remote.accessCount = accessCount1;
                if (doorCount == 1)
                {
                    remote.checkBox1.Enabled = true;
                    remote.checkBox2.Enabled = false;
                    remote.checkBox3.Enabled = false;
                    remote.checkBox4.Enabled = false;
                }
                else if (doorCount == 2)
                {
                    remote.checkBox1.Enabled = true;
                    remote.checkBox2.Enabled = true;
                    remote.checkBox3.Enabled = false;
                    remote.checkBox4.Enabled = false;
                }
                else
                {
                    remote.checkBox1.Enabled = true;
                    remote.checkBox2.Enabled = true;
                    remote.checkBox3.Enabled = true;
                    remote.checkBox4.Enabled = true;
                }
                remote.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                string FormTitle=this.Text;

                CCorrectTime jzsj = new CCorrectTime();
                jzsj.temp = s;

                jzsj.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button12_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                string FormTitle = this.Text;
                CDlgGrant sq = new CDlgGrant();
                sq.ip = ip;
                sq.mac = mac;
                sq.sn = sn;
                sq.password = password;
                sq.gateway = gateway;
                sq.doorCount = doorCount;
                sq.tcpport = tcpport;
                sq.udpport = udpport;
                sq.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button13_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                string FormTitle = this.Text;
                CSetOpenPassword sq = new CSetOpenPassword();
                sq.ip = ip;
                sq.mac = mac;
                sq.sn = sn;
                sq.password = password;
                sq.gateway = gateway;
                sq.doorCount = doorCount;
                sq.tcpport = tcpport;
                sq.udpport = udpport;
                sq.mode = mode;
                sq.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button19_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                string FormTitle = this.Text;
                CFireAlarm fire = new CFireAlarm();
                fire.ip = ip;
                fire.mac = mac;
                fire.sn = sn;
                fire.password = password;
                fire.gateway = gateway;
                fire.doorCount = doorCount;
                fire.tcpport = tcpport;
                fire.udpport = udpport;
                fire.mode = mode;
                fire.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;

        }

        private void button18_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                string FormTitle = this.Text;
                CPoliceAlarm police = new CPoliceAlarm();
                police.ip = ip;
                police.mac = mac;
                police.sn = sn;
                police.password = password;
                police.gateway = gateway;
                police.doorCount = doorCount;
                police.tcpport = tcpport;
                police.udpport = udpport;
                police.mode = mode;
                police.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button21_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                string FormTitle = this.Text;
                CGasAlarm gas = new CGasAlarm();
                gas.ip = ip;
                gas.mac = mac;
                gas.sn = sn;
                gas.password = password;
                gas.gateway = gateway;
                gas.doorCount = doorCount;
                gas.tcpport = tcpport;
                gas.udpport = udpport;
                gas.mode = mode;
                gas.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button20_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                string FormTitle = this.Text;
                CProBE probe = new CProBE();
                probe.ip = ip;
                probe.mac = mac;
                probe.sn = sn;
                probe.password = password;
                probe.gateway = gateway;
                probe.doorCount = doorCount;
                probe.tcpport = tcpport;
                probe.udpport = udpport;
                probe.mode = mode;
                probe.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button22_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                string FormTitle = this.Text;
                CUnregCard uCard = new CUnregCard();
                uCard.ip = ip;
                uCard.mac = mac;
                uCard.sn = sn;
                uCard.password = password;
                uCard.gateway = gateway;
                uCard.doorCount = doorCount;
                uCard.tcpport = tcpport;
                uCard.udpport = udpport;
                uCard.mode = mode;
                uCard.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }
        public int resevedataType = 1;
        ConnectMain mIo = null;
        private void Form1_Load(object sender, EventArgs e)
        {
            //设定列不能自动作成  
            dataGridView1.AllowUserToAddRows = false;

            DataGridViewCheckBoxColumn acCode0 = new DataGridViewCheckBoxColumn();
            acCode0.Name = "choose";
            acCode0.DataPropertyName = "choose";
            acCode0.HeaderText = "choose";
            acCode0.TrueValue = true;
            acCode0.FalseValue = false;
            acCode0.Width = 60;
            dataGridView1.Columns.Add(acCode0);
            DataGridViewTextBoxColumn acCode = new DataGridViewTextBoxColumn();
            acCode.Name = "IP";
            acCode.DataPropertyName = "IP";
            acCode.HeaderText = "IP";
            dataGridView1.Columns.Add(acCode);
            DataGridViewTextBoxColumn acCode1 = new DataGridViewTextBoxColumn();
            acCode1.Name = "Mac";
            acCode1.DataPropertyName = "Mac";
            acCode1.HeaderText = "Mac";
            dataGridView1.Columns.Add(acCode1);
            DataGridViewTextBoxColumn acCode2 = new DataGridViewTextBoxColumn();
            acCode2.Name = "SN";
            acCode2.DataPropertyName = "SN";
            acCode2.HeaderText = "SN";
            dataGridView1.Columns.Add(acCode2);
            DataGridViewTextBoxColumn acCode3 = new DataGridViewTextBoxColumn();
            acCode3.Name = "Communication password";
            acCode3.DataPropertyName = "Communication password";
            acCode3.HeaderText = "Communication password";
            dataGridView1.Columns.Add(acCode3);
            DataGridViewTextBoxColumn acCode4 = new DataGridViewTextBoxColumn();
            acCode4.Name = "TCP port";
            acCode4.DataPropertyName = "TCP port";
            acCode4.HeaderText = "TCP port";
            dataGridView1.Columns.Add(acCode4);
            DataGridViewTextBoxColumn acCode5 = new DataGridViewTextBoxColumn();
            acCode5.Name = "UDP port";
            acCode5.DataPropertyName = "UDP port";
            acCode5.HeaderText = "UDP port";
            dataGridView1.Columns.Add(acCode5);
            DataGridViewTextBoxColumn acCode6 = new DataGridViewTextBoxColumn();
            acCode6.Name = "Gateway";
            acCode6.DataPropertyName = "Gateway";
            acCode6.HeaderText = "Gateway";
            dataGridView1.Columns.Add(acCode6);
            DataGridViewTextBoxColumn acCode7 = new DataGridViewTextBoxColumn();
            acCode7.Name = "Operating mode";
            acCode7.DataPropertyName = "Operating mode";
            acCode7.HeaderText = "Operating mode";
            dataGridView1.Columns.Add(acCode7);
            dataGridView1.Columns[4].Visible = false;
            dataGridView1.Columns[5].Visible = false;
            dataGridView1.Columns[6].Visible = false;
            dataGridView1.Columns[7].Visible = false;

            resevedataType = 1;
            //Initialize the network环境
            IntPtr w2 = cl2.initNet("C#");
            if (mIo == null)
            {
                mIo = new ConnectMain();
                mIo.CommandAchieve += _CommandAchieve;//Command success event
                mIo.CommandTimeout += _CommandTimeout;//Command timeout event
                mIo.ConnectError += _ConnectError;//Connection error event
                mIo.PasswordError += _PasswordError;//Password error event
                dInfo.OnInput += cl_OnInput;//Used to transmit real-time monitoring events
            }

            //Receive the returned search device information and display it to the dataGridview
            searchInfo += cl_search;
        }

        #region Event
        private void _PasswordError(ConnectInfo oInfo, int iCommandCode, object oValue)
        {

        }
        private void _ConnectError(ConnectInfo oInfo, int iCommandCode, object oValue)
        {
            if (iCommandCode == (int)ConnectMain.eCommandCode.cmdSearchEquptOnNetNum)
            {
                _searchCount = _searchCountMax;//Stop command, the number of searches reaches the maximum to stop the search
                this.Invoke(new Action(SearchEquptOver));
            }
        }

        private void _CommandTimeout(ConnectInfo oInfo, int iCommandCode, int iStep, object oValue)
        {
            if (iCommandCode == (int)ConnectMain.eCommandCode.cmdSearchEquptOnNetNum)
            {
                this.Invoke(new Action(SearchEquptOver));//End the search and process the searched devices
            }
        }

        private void _CommandAchieve(ConnectInfo oInfo, int iCommandCode, object oValue)
        {
            //ConnectInfo oInfo, int iCommandCode, object oValue
            if (FunList != null)
            {
                if (FunList.ContainsKey(iCommandCode))
                    FunList[iCommandCode](oInfo, oValue);//Execute the delegate corresponding to the command code in the dictionary
            }
        }


        #endregion
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            //Clean up the network environment
            cl2.clearNet();
            //Release all memory
            ClearMemory();
        }
        public static void ClearMemory()
        {  
            GC.Collect();  
            GC.WaitForPendingFinalizers(); 
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)  
            {  
                cl2.SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);  
            }
            Process.GetCurrentProcess().Kill();
        }

        int col = 0;
        int index = 0;
        const int SEND_PATH = 0x004A;

        private const int WM_COPYDATA = 0x004A;  //Message type 
        private const int WM_MSG_USER = 100;  //Message type 
        public struct CopyDataStruct
        {
            public IntPtr dwData; // Additional parameters 
            public int cbData; // Data size
            public IntPtr lpData; // Data content
        }

        long _searchNetNum = 0;
        int _searchCountMax = 0;
        int _searchCount = 0;
        List<SearchData> _searchSN = null;
        List<string> _listInfo = new List<string>();//Store the received controller information
        private class SearchData
        {
            public FCardCDrive.TCPInfo_FC8000 TCP;
            public string SN;
            public byte[] SNByte;
            public SearchData(string sSN, byte[] bSNByte, TCPInfo_FC8000 oTCP)
            {
                TCP = oTCP;
                SN = sSN;
                SNByte = bSNByte;
            }
        }
        public event EventHandler<MyEventArg2> searchInfo;//Define the device used to display the search
        private void button1_Click(object sender, EventArgs e)
        {
            resevedataType = 1;
            if (!radioButton1.Checked && !radioButton2.Checked)
            {
                MessageBox.Show("Please select the communication method");
            }
            else
            {
                if (radioButton1.Checked)
                {
                    if(textBox1.Text==""||textBox2.Text=="")
                    {
                        MessageBox.Show(" port number cannot be empty");
                    }
                    else
                    {
                        dataGridView1.Rows.Clear();
                        string z = this.Text;
                        long lNetNum = 0;
                        Random rand = new Random();
                        lNetNum = rand.Next(1, 65535);
                        _searchNetNum = lNetNum;
                        _searchCountMax = 3;
                        _searchCount = 1;
                        if (_searchSN == null)
                        {
                            _searchSN = new List<SearchData>();
                        }
                        _searchSN.Clear();
                        ConnectInfo oInfo = GetConnInfo(0, 5000);
                        if (mIo.Command(oInfo, "SearchEquptOnNetNum", lNetNum, 1))
                        {
                            button1.Enabled = false;
                        }

                        AddProcessFun((int)ConnectMain.eCommandCode.cmdSearchEquptOnNetNum, SearchEquptStep);


                    }
                }
                else
                {
                    MessageBox.Show("485 communication interface is still being improved...");
                }
            }
        }
        #region Event Search device
        public delegate void MyInvoke();
        delegate void CmdAchieve(ConnectInfo oInfo, object oValue);//Event handling delegate 
        Dictionary<int, CmdAchieve> FunList = null;//Command code & delegate object
        public Thread invokeThread;
        //Search process
        private void AddProcessFun(int key, CmdAchieve val)
        {
            if (FunList == null)
                FunList = new Dictionary<int, CmdAchieve>();
            //Determine whether this command code exists
            if (!FunList.ContainsKey(key))
            {
                FunList.Add(key, val);
            }
        }
        public class MyEventArg2 : EventArgs
        {
            List<string> _msg;
            public MyEventArg2(List<string> msg)
            {
                this._msg = msg;
            }
            public List<string> Msg
            {
                get { return  _msg; }
            }
        }
        private void SearchEquptStep(ConnectInfo oInfo, object oValue)
        {
            //Device found
            bool bSave = false;

            TCPInfo_FC8000 oTCP = (TCPInfo_FC8000)GetObjValue(oValue, "TCP");
            string sSearchSN = (string)GetObjValue(oValue, "SN");
            byte[] bSN = (byte[])GetObjValue(oValue, "SNByte");
            bSave = true;


            _listInfo = new List<string>();
            _listInfo.Add(oTCP.IP);//IP
            _listInfo.Add(oTCP.MAC);//Mac
            _listInfo.Add(sSearchSN);//SN
            _listInfo.Add("ffffffff");//Communication password
            _listInfo.Add(Convert.ToString(oTCP.TCPPort));//TCP port
            _listInfo.Add(Convert.ToString(oTCP.UDPPort));//UDP port
            _listInfo.Add(oTCP.IPGateway);//Gateway
            _listInfo.Add("server");//Operating mode

            //Use the delegate method to operate the dataGridview in multithreading, and transfer the searched information to the dataGridview
            searchInfo(this, new MyEventArg2(_listInfo));

            foreach (SearchData oSN in _searchSN)
            {
                if (oSN.SNByte.BytesEquals(bSN))
                {
                    //Determine whether this device already exists
                    bSave = false;
                    break; // TODO: might not be correct. Was : Exit For
                }
            }

            if (bSave)
            {
                //Add to the collection and send setting instructions
                SearchData oSN = new SearchData(sSearchSN, bSN, oTCP);
                _searchSN.Add(oSN);
                ConnectInfo oSnedInfo = GetConnInfo(iTimeout: 5000);
                oSnedInfo.SN = sSearchSN;
                mIo.Command(oSnedInfo, "SetEquptNetNum", _searchNetNum, 1);

                _searchCountMax = _searchCount + 3;
                //往后延伸三次
                if (_searchCountMax > 10)
                {
                    _searchCountMax = 10;
                }

            }
        }
        public object GetObjValue(object obj, string PropertyName)
        {
            Type type = obj.GetType();
            object ret = type.GetProperty(PropertyName).GetValue(obj, null);
            if (ret.GetType().FullName == "System.UInt32")
                ret = (int)(uint)ret;
            return ret;
            object val = null;
            if (!ret.GetType().IsGenericType)
            {
                //Non-generic
                val = Convert.ChangeType(ret, ret.GetType());//PropertyType);
                //default()

            }
            else
            {
                Type genericTypeDefinition = ret.GetType().GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(Nullable<>))
                {
                    val = Convert.ChangeType(ret, Nullable.GetUnderlyingType(ret.GetType()));
                }
            }
            return val;
        }
        private ConnectInfo GetConnInfo(int RestartCount = 1, int iTimeout = 1500)
        {
            ConnectInfo oInfo = new ConnectInfo();

            oInfo.ConnType = ConnectInfo.e_ConnectType.OnUDP;
            oInfo.IP = "";
            oInfo.NetPort = 8101;
            oInfo.UDPBroadcast = true;

            oInfo.EquptType = ConnectInfo.e_EquptType.FC8900;
            oInfo.SN = "MC-5824T25070244";
            oInfo.Password = "FFFFFFFF";

            oInfo.RestartCount = RestartCount;
            oInfo.TimeOutMSEL = iTimeout;
            return oInfo;
        }
        private void SearchEquptOver()
        {
            //搜索结束
            if (_searchCount < _searchCountMax)
            {
                //继续搜索
                _searchCount += 1;
                mIo.Command(GetConnInfo(0, 5000), "SearchEquptOnNetNum", _searchNetNum, 1);
            }
            else
            {
                button1.Enabled = true;
            }
        }
        #endregion //Entire search
        protected override void DefWndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_MSG_USER://Back to search results
                    {
                        if (resevedataType == 1)
                        {
                            //CopyDataStruct vCopyDataStruct = (CopyDataStruct)Marshal.PtrToStructure(
                            //    m.LParam, typeof(CopyDataStruct));
                            //string receive = Marshal.PtrToStringAuto(vCopyDataStruct.dwData);
                            //this.dataGridView1.Rows[index].Cells[col+1].Value = receive;
                            //if (col == 7)
                            //{
                            //    col = 0;
                            //    index = this.dataGridView1.Rows.Add();
                            //    //dataGridView1.Rows[index].ReadOnly = true;
                            //    //dataGridView1.Rows.Remove(dataGridView1.SelectedRows[index-1]);
                            //}
                            //else
                            //{
                            //    col++;
                            //}

                        }
                        else if (resevedataType == 2)
                        {
                            CopyDataStruct vCopyDataStruct = (CopyDataStruct)Marshal.PtrToStructure(
                                m.LParam, typeof(CopyDataStruct));
                            string result = Marshal.PtrToStringAuto(vCopyDataStruct.dwData);
                            textBox3.Text = result + "\r\n" + textBox3.Text;
                            if (textBox3.Text.Length>1000)
                            {
                                textBox3.Text = result + "\r\n";
                            }
                        }
                    }
                    //this.textBox1.Text = cdata.lpData;
                    break;
                default:
                    base.DefWndProc(ref m);
                    break;
            }
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if(!(Char.IsNumber(e.KeyChar))&&e.KeyChar!=(char)8)
            {
                e.Handled = true;
                MessageBox.Show("Please key in numbers!");
            }
            else
            {
            }

        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!(Char.IsNumber(e.KeyChar)) && e.KeyChar != (char)8)
            {
                e.Handled = true;
                MessageBox.Show("Please key in numbers!");
            }
            else
            {
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {

            //string wz = cl2.devInfo2();
            //MessageBox.Show(wz);
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                IntPtr result = cl2.install(true, s, doorCount);
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button11_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string FormTitle = this.Text;
                CTimeSetting ct = new CTimeSetting();
                ct.Text = "Timing normally open setting";
                ct.ip = ip;
                ct.mac = mac;
                ct.sn = sn;
                ct.password = password;
                ct.gateway = gateway;
                ct.doorCount = doorCount;
                ct.tcpport = tcpport;
                ct.udpport = udpport;
                ct.mode = mode;
                ct.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button10_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string FormTitle = this.Text;
                CTimeSetting ct = new CTimeSetting();
                ct.Text = "Opening hours setting";
                ct.ip = ip;
                ct.mac = mac;
                ct.sn = sn;
                ct.password = password;
                ct.gateway = gateway;
                ct.doorCount = doorCount;
                ct.tcpport = tcpport;
                ct.udpport = udpport;
                ct.mode = mode;
                ct.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button15_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();

                string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                string info = "";
                try
                {
                    string result = cl2.getDevInfo(sn, ip, Convert.ToInt16(tcpport), password, info);
                    MessageBox.Show(result);
                }
                catch
                {
                    MessageBox.Show("Please select a controller");
                }
                return;
            }
            else
            {
                MessageBox.Show("Please select a controller");
                return;
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string FormTitle = this.Text;
                CSetAntiSneakBack anti = new CSetAntiSneakBack();
                anti.ip = ip;
                anti.mac = mac;
                anti.sn = sn;
                anti.password = password;
                anti.gateway = gateway;
                anti.doorCount = doorCount;
                anti.tcpport = tcpport;
                anti.udpport = udpport;
                anti.mode = mode;
                anti.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button16_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string FormTitle = this.Text;
                CMagAlarm anti = new CMagAlarm();
                anti.ip = ip;
                anti.mac = mac;
                anti.sn = sn;
                anti.password = password;
                anti.gateway = gateway;
                anti.doorCount = doorCount;
                anti.tcpport = tcpport;
                anti.udpport = udpport;
                anti.mode = mode;
                anti.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }
        public static string[] dataMonitorItems = new string[4 * 1000];
        public static int accessCount1 = 0;
        public static int monitor = 0;
        public DataMonitor dInfo = new DataMonitor();//Real-time monitoring
        private void button14_Click(object sender, EventArgs e)
        {
            dInfo._listInfo = new List<string>();
            dInfo.Init();
            resevedataType = 2;
            int countIndex = 0;
            int accseeCount=0;//Number of controllers
            string[] mf4 = new string[4 * 1000];
            for (int i = 0; i < this.dataGridView1.Rows.Count; i++)
            {
                if ((bool)dataGridView1.Rows[i].Cells["choose"].EditedFormattedValue == true)
                {
                    dInfo._listInfo.Add(dataGridView1.Rows[i].Cells[3].Value.ToString());//sn;
                    dInfo._listInfo.Add(dataGridView1.Rows[i].Cells[1].Value.ToString());//ip
                    dInfo._listInfo.Add(dataGridView1.Rows[i].Cells[5].Value.ToString());// port
                    dInfo._listInfo.Add(dataGridView1.Rows[i].Cells[4].Value.ToString());//Communication password
                    countIndex=countIndex+4;
                    accseeCount++;
                }
            }
            //string[] s = { "s", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, "255.255.255.0", null, "0.0.0.0", "0.0.0.0", null, null, null };
            string FormText = this.Text;
            if (button14.Text == "Start monitoring")
            {
                IntPtr result;
                dInfo.OpenMonitor();
                accessCount1 = accseeCount;

                button14.Text = "Stop monitoring";
                monitor = 1;
            }
            else if (button14.Text == "Stop monitoring")
            {
                dInfo.CloseMonitor();

                button14.Text = "Start monitoring";
                monitor = 0;

            }
            return;
        }

        //Receive monitoring data returned by the DataMonitor class
        void cl_OnInput(object sender, MyEventArg e)
        {
            this.Invoke(new EventHandler(delegate { textBox3.Text = e.Msg + textBox3.Text; }));           
        }
        //Receive the returned searched controller
        void cl_search(object sender, MyEventArg2 e)
        {
            this.Invoke(new EventHandler(delegate {
                int index1 = dataGridView1.RowCount;
                int www = _listInfo.Count();
                for (int i = 0; i < _listInfo.Count() / 8; i++)
                {
                    this.dataGridView1.Rows.Add();
                    for (int j = 0; j < 8; j++)
                    {
                        if (_listInfo[i * 8 + j].Replace(" ", "") != "")
                        {
                            this.dataGridView1.Rows[index1].Cells[j + 1].Value = _listInfo[j];
                        }
                    }
                }
            }));           
        }
        private void button17_Click(object sender, EventArgs e)
        {
            string[] outvalue = new string[52000];
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport };
                string info = "";
                CGetRecord getRecord = new CGetRecord();
                getRecord.ip = ip;
                getRecord.mac = mac;
                getRecord.sn = sn;
                getRecord.password = password;
                getRecord.gateway = gateway;
                getRecord.doorCount = doorCount;
                getRecord.tcpport = tcpport;
                getRecord.udpport = udpport;
                getRecord.mode = mode;
                getRecord.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button5_Click_1(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string FormTitle = this.Text;
                MutilCard mutiliCard = new MutilCard();
                mutiliCard.ip = ip;
                mutiliCard.mac = mac;
                mutiliCard.sn = sn;
                mutiliCard.password = password;
                mutiliCard.gateway = gateway;
                mutiliCard.doorCount = doorCount;
                mutiliCard.tcpport = tcpport;
                mutiliCard.udpport = udpport;
                mutiliCard.mode = mode;
                mutiliCard.monitor = monitor;
                mutiliCard.accessCount = accessCount1;
                if (doorCount == 1)
                {
                    mutiliCard.checkBox1.Enabled = true;
                    mutiliCard.checkBox2.Enabled = false;
                    mutiliCard.checkBox3.Enabled = false;
                    mutiliCard.checkBox4.Enabled = false;
                }
                else if (doorCount == 2)
                {
                    mutiliCard.checkBox1.Enabled = true;
                    mutiliCard.checkBox2.Enabled = true;
                    mutiliCard.checkBox3.Enabled = false;
                    mutiliCard.checkBox4.Enabled = false;
                }
                else
                {
                    mutiliCard.checkBox1.Enabled = true;
                    mutiliCard.checkBox2.Enabled = true;
                    mutiliCard.checkBox3.Enabled = true;
                    mutiliCard.checkBox4.Enabled = true;
                }
                mutiliCard.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

        private void button23_Click(object sender, EventArgs e)
        {
            //Used to judge whether it is a single controller interlock or a cross-area interlock
            int accessCount = 0;//选中的Number of controllers
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                if ((bool)dataGridView1.Rows[i].Cells[0].EditedFormattedValue == true)
                {
                    //TODO
                    accessCount++;
                }

            }
            if (accessCount>1)//Area
            {
                AreaMultiDoorInterlock area = new AreaMultiDoorInterlock();
                area.ShowDialog();
            }
            else//Single controller
            {
                int count = dataGridView1.RowCount;


                if (count == 0)
                {
                    return;
                }
                int index = dataGridView1.CurrentRow.Index;
                if (dataGridView1.Rows[index].Cells[1].Value == null)
                {
                    return;
                }
                if (index >= 0)
                {
                    string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                    string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                    string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                    string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                    string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                    int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                    string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                    string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                    string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                    string FormTitle = this.Text;
                    SimpleMultiDoorInterlock remote = new SimpleMultiDoorInterlock();
                    remote.ip = ip;
                    remote.mac = mac;
                    remote.sn = sn;
                    remote.password = password;
                    remote.gateway = gateway;
                    remote.doorCount = doorCount;
                    remote.tcpport = tcpport;
                    remote.udpport = udpport;
                    remote.mode = mode;

                    if (doorCount == 1)
                    {
                        remote.checkBox1.Enabled = true;
                        remote.checkBox2.Enabled = false;
                        remote.checkBox3.Enabled = false;
                        remote.checkBox4.Enabled = false;
                    }
                    else if (doorCount == 2)
                    {
                        remote.checkBox1.Enabled = true;
                        remote.checkBox2.Enabled = true;
                        remote.checkBox3.Enabled = false;
                        remote.checkBox4.Enabled = false;
                    }
                    else
                    {
                        remote.checkBox1.Enabled = true;
                        remote.checkBox2.Enabled = true;
                        remote.checkBox3.Enabled = true;
                        remote.checkBox4.Enabled = true;
                    }
                    remote.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Please select a controller");
                }
                return;
            }
        }

        private void button24_Click(object sender, EventArgs e)
        {
            //Used to judge whether it is a single controller anti-passback or cross-area anti-passback
            int accessCount = 0;//选中的Number of controllers
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                if ((bool)dataGridView1.Rows[i].Cells[0].EditedFormattedValue == true)
                {
                    //TODO
                    accessCount++;
                }

            }
            if (accessCount>1)//Area
            {
                AreaAntiBack area = new AreaAntiBack();
                area.ShowDialog();
            }
            else//Single controller
            {
                int count = dataGridView1.RowCount;


                if (count == 0)
                {
                    return;
                }
                int index = dataGridView1.CurrentRow.Index;
                if (dataGridView1.Rows[index].Cells[1].Value == null)
                {
                    return;
                }
                if (index >= 0)
                {
                    string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                    string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                    string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                    string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                    string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                    int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                    string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                    string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                    string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                    string FormTitle = this.Text;
                    SimpleAntiBack remote = new SimpleAntiBack();
                    remote.ip = ip;
                    remote.mac = mac;
                    remote.sn = sn;
                    remote.password = password;
                    remote.gateway = gateway;
                    remote.doorCount = doorCount;
                    remote.tcpport = tcpport;
                    remote.udpport = udpport;
                    remote.mode = mode;

                    if (doorCount == 1)
                    {
                        remote.checkBox1.Enabled = true;
                        remote.checkBox2.Enabled = false;
                        remote.checkBox3.Enabled = false;
                        remote.checkBox4.Enabled = false;
                    }
                    else if (doorCount == 2)
                    {
                        remote.checkBox1.Enabled = true;
                        remote.checkBox2.Enabled = true;
                        remote.checkBox3.Enabled = false;
                        remote.checkBox4.Enabled = false;
                    }
                    else
                    {
                        remote.checkBox1.Enabled = true;
                        remote.checkBox2.Enabled = true;
                        remote.checkBox3.Enabled = true;
                        remote.checkBox4.Enabled = true;
                    }
                    remote.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Please select a controller");
                }
                return;
            }
        }

        private void button25_Click(object sender, EventArgs e)
        {
            int count = dataGridView1.RowCount;
            if (count == 0)
            {
                return;
            }
            int index = dataGridView1.CurrentRow.Index;
            if (dataGridView1.Rows[index].Cells[1].Value == null)
            {
                return;
            }
            if (index >= 0)
            {
                string ip = dataGridView1.Rows[index].Cells[1].Value.ToString();
                string mac = dataGridView1.Rows[index].Cells[2].Value.ToString();
                string sn = dataGridView1.Rows[index].Cells[3].Value.ToString();
                string password = dataGridView1.Rows[index].Cells[4].Value.ToString();
                string gateway = dataGridView1.Rows[index].Cells[7].Value.ToString();
                int doorCount = Convert.ToInt16(dataGridView1.Rows[index].Cells[3].Value.ToString().Substring(5, 1));
                string tcpport = dataGridView1.Rows[index].Cells[5].Value.ToString();
                string udpport = dataGridView1.Rows[index].Cells[6].Value.ToString();
                string mode = dataGridView1.Rows[index].Cells[8].Value.ToString();
                string FormTitle = this.Text;
                CGetAccessInfo getAccessInfo = new CGetAccessInfo();
                if (monitor == 1)//Data monitoring has been turned on
                {
                    getAccessInfo.mIO = demo.DataMonitor.mIO;
                }
                //getAccessInfo.mIO = demo.DataMonitor.mIO;
                getAccessInfo.ip = ip;
                getAccessInfo.mac = mac;
                getAccessInfo.sn = sn;
                getAccessInfo.password = password;
                getAccessInfo.gateway = gateway;
                getAccessInfo.doorCount = doorCount;
                getAccessInfo.tcpport = tcpport;
                getAccessInfo.udpport = udpport;
                getAccessInfo.mode = mode;
                getAccessInfo.monitor = 0;
                getAccessInfo.accessCount = accessCount1;
                getAccessInfo.ShowDialog();
            }
            else
            {
                MessageBox.Show("Please select a controller");
            }
            return;
        }

    }
    public class cl2
    {
        //Release all memory
        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize")]  
        public static extern int SetProcessWorkingSetSize(IntPtr process, int minSize, int maxSize);  
        //Initialize the network
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr initNet(String title);
        //Clean up the network when exit
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr clearNet();
        //Search
        //Cancel the searchDev interface. For specific usage, refer to the "EventHandler" class defined by "button1_Click"
        //Initialize the controller
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr install(bool initialize, string[] s, int door);
        //Read time
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern string readDevNowTime(string[] s, string info);
        //addressIp:serverIP，addressPort:server port号，enabledAddress:是否Enableserver接收
        //Calibration time
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr calibrationTime(string[] s);
        //Modify IP
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr updateIP(string[] s);
        //Update door open delay
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr openTimeDelay(string[] s, int optFlag, string[] netIP, string openDoorTime);
        //remote open
        [DllImport("CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr remoteOpen(string sn, string ip, int port, string pwd, int[] portNum, int accessCount, int monitor);
        //Opening hours setting
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr setTimes(string[] s, int optFlag, int timeNum, string timePieces);
        //Timing normally open setting
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr regularTime(string[] s, int optFlag, string[] netIP, string[] oftenopenDoorTime);
        //Add authorization card
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr addUnSortCard(string DevSN, string ip, int port, string password, int cardCount, string cardNo,
            string cardPassword, int openmode, IntPtr ioFlag, int openTimeCount, string openLock, string permitTime
            , string timePieceIndex, int holidayEnable);
        //Set door password
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr setOpenDoorPwd(string[] s,string openlock);
        //Read door password
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr readOpenDoorPwd(string[] s, string openlock);
        //Clear the door code
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr clearAllDoorPwd(string[] s);
        //Read hardware version, running days
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern string getDevInfo(string devSN, string ip, int port, string password, string info);
        //Set anti-passback
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr setAntiSneakBack(int zt, string[] s, string doorselect);
        //Alarm mode setting, trigger, and release
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr policeOfficer(string[] s, int alarmAction);
        //Refresh the alarm status
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern string fireAlarm(string[] s, string FormText);
        //real time monitoring

        //Extract records
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern int getRecord(string devSN, string ip, int port, string password, int recTypeIndex);
        //addressIp: serverIP, addressPort: server port number, enabledAddress: whether to enable server reception
        //Used for Search device callback
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern string devInfo2();
        //Multi-card door opening verification mode
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern int MultiCardVerifyMode(string[] s, int portNum, int creditMode, int antiMode);
        //portNum: door number, creditMode: verification mode that encountered an error, antiMode: whether to detect anti-passback

        //Verify door opening mode
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern int MultiCardOpenMode(string[] s, int portNum, int creditMode, int countA, int countB);
        //portNum: door number, creditMode: verification mode that encountered an error, antiMode: whether to detect anti-passback

        //Group AB door opening mode
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern int ABOpenMode(string[] s, int groupType, int groupNumber, int cardCount, string cardInfo);
        //portNum: door number, creditMode: verification mode that encountered an error, antiMode: whether to detect anti-passback

        //Fixed group door opening mode
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern int FixOpenMode(string[] s, int portNum, int groupType, int groupNumber, int cardCount, string cardInfo);
        //portNum: door number, creditMode: verification mode that encountered an error, antiMode: whether to detect anti-passback
        //Query verification mode
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern string MultiGetCardVerifyMode(string[] s, int portNum,string info);
        //portNum:door number

        //Query multi-card combination verification mode
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern string getGroupModeInfo(string[] s, int portNum, string info);
        //portNum:door number


        //Single controller multi-door interlock
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern IntPtr simpleInterlock(string[] s, int[] portNum);
        //portNum:door number


        //Multi-door interlock across controllers
        [DllImport(@"CareaIfc.dll", CharSet = CharSet.Unicode, ExactSpelling = false, CallingConvention = CallingConvention.Cdecl)]
        unsafe public static extern int AreaInterlock(string[] s, int enabled, int type, int portNum, string homeSN, string homeIP,int num);
        //portNum:door number
    }
}