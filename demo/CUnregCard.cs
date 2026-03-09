using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace demo
{
    public partial class CUnregCard : Form
    {
        public CUnregCard()
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
        public string openPassword;
        public string mode;

        private const int WM_COPYDATA = 0x004A;  //Message type 
        private const int WM_READ_ALARM_STATIC=100;  //Message type 



        private void CUnregCard_Load(object sender, EventArgs e)
        {
            comboBox1.Items.Add("Off");
            comboBox1.Items.Add("On");
            if (doorCount == 1)
            {
                button1.Enabled = true;
                button5.Enabled = true;
                button3.Enabled = true;
                button2.Enabled = false;
                button7.Enabled = false;
                button6.Enabled = false;
                button8.Enabled = false;
                button10.Enabled = false;
                button9.Enabled = false;
                button11.Enabled = false;
                button13.Enabled = false;
                button12.Enabled = false;
            }
            else if (doorCount == 2)
            {
                button1.Enabled = true;
                button5.Enabled = true;
                button3.Enabled = true;
                button2.Enabled = true;
                button7.Enabled = true;
                button6.Enabled = true;
                button8.Enabled = false;
                button10.Enabled = false;
                button9.Enabled = false;
                button11.Enabled = false;
                button13.Enabled = false;
                button12.Enabled = false;
            }
            else
            {
                button1.Enabled = true;
                button5.Enabled = true;
                button3.Enabled = true;
                button2.Enabled = true;
                button7.Enabled = true;
                button6.Enabled = true;
                button8.Enabled = true;
                button10.Enabled = true;
                button9.Enabled = true;
                button11.Enabled = true;
                button13.Enabled = true;
                button12.Enabled = true;
            }
        }


        public void button10_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "3", "5", "3", "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            string alarmStatus = "";
            alarmStatus = cl2.fireAlarm(s, alarmStatus);
            textBox1.Text = alarmStatus;
        }

        public void button9_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "3", "5", "3", "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            IntPtr result = cl2.policeOfficer(s, 3);
        }
        public void button11_Click(object sender, EventArgs e)
        {
            int index = comboBox1.SelectedIndex;
            if (index == -1)
            {
                MessageBox.Show("Please choose the alarm mode");
            }
            else
            {
                string[] s = { mac, ip, tcpport, sn, password, "4", "5", Convert.ToString(index), "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                IntPtr result = cl2.policeOfficer(s, 1);
            }
        }
        public void button12_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "4", "5", "3", "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            IntPtr result = cl2.policeOfficer(s, 3);
        }
        public void button13_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "4", "5", "3", "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            string alarmStatus = "";
            alarmStatus = cl2.fireAlarm(s, alarmStatus);
            textBox1.Text = alarmStatus;
        }
        public void button2_Click(object sender, EventArgs e)
        {
            int index = comboBox1.SelectedIndex;
            if (index == -1)
            {
                MessageBox.Show("Please choose the alarm mode");
            }
            else
            {
                string[] s = { mac, ip, tcpport, sn, password, "2", "5", Convert.ToString(index), "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                IntPtr result = cl2.policeOfficer(s, 1);
            }
        }
        public void button6_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "2", "5", "3", "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            IntPtr result = cl2.policeOfficer(s, 3);
        }
        public void button7_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "2", "5", "3", "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            string alarmStatus = "";
            alarmStatus = cl2.fireAlarm(s, alarmStatus);
            textBox1.Text = alarmStatus;
        }
        public void button8_Click(object sender, EventArgs e)
        {
            int index = comboBox1.SelectedIndex;
            if (index == -1)
            {
                MessageBox.Show("Please choose the alarm mode");
            }
            else
            {
                string[] s = { mac, ip, tcpport, sn, password, "3", "5", Convert.ToString(index), "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                IntPtr result = cl2.policeOfficer(s, 1);
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            int index = comboBox1.SelectedIndex;
            if (index == -1)
            {
                MessageBox.Show("Please choose the alarm mode");
            }
            else
            {
                string[] s = { mac, ip, tcpport, sn, password, "1", "5", Convert.ToString(index), "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
                //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                IntPtr result = cl2.policeOfficer(s, 1);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "1", "5", "3", "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            IntPtr result = cl2.policeOfficer(s, 3);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "1", "5", "3", "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            string alarmStatus = "";
            alarmStatus = cl2.fireAlarm(s, alarmStatus);
            textBox1.Text = alarmStatus;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
