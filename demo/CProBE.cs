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
    public partial class CProBE : Form
    {
        public CProBE()
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

        private void CProBE_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox2.Text != "" || textBox3.Text != "" || textBox4.Text != "" || textBox5.Text != ""|| textBox6.Text != "")
            {
                if(textBox4.Text !=textBox5.Text)
                {
                    string[] s = { mac, ip, tcpport, sn, password, "0", "3", "4", textBox2.Text, textBox3.Text, textBox5.Text, textBox4.Text, textBox6.Text, "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
                    //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
                    IntPtr result = cl2.policeOfficer(s, 1);
                }
                else
                {
                    MessageBox.Show("Arming password and Disarming password cannot be the same");
                }
            }
            else
            {
                MessageBox.Show("Entry delay, alarm delay, arming password, Disarming password, Alarm hold time cannot be empty");
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "0", "3", "4", textBox2.Text, textBox3.Text, textBox5.Text, textBox4.Text, textBox6.Text, "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            IntPtr result = cl2.policeOfficer(s, 4);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "0", "3", "4", textBox2.Text, textBox3.Text, textBox5.Text, textBox4.Text, textBox6.Text, "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            IntPtr result = cl2.policeOfficer(s, 5);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "0", "3", "4", textBox2.Text, textBox3.Text, textBox5.Text, textBox4.Text, textBox6.Text, "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            string alarmStatus = "";
            alarmStatus = cl2.fireAlarm(s, alarmStatus);
            textBox1.Text = alarmStatus;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, "0", "3", "4", "", "", "", "", "", "", "", "", mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            //string[] s = { sn, ip, "255.255.255.0", "192.168.11.1", "0.0.0.0", "0.0.0.0", "server", Convert.ToString(8000), Convert.ToString(8101), password };
            IntPtr result = cl2.policeOfficer(s, 3);
        }
    }
}
