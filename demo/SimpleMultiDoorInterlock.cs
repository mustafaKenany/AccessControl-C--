using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace demo
{
    public partial class SimpleMultiDoorInterlock : Form
    {
        public SimpleMultiDoorInterlock()
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
        private void button1_Click(object sender, EventArgs e)
        {
            int[] doorCheck = new int[4];
            if (!checkBox1.Checked && !checkBox2.Checked && !checkBox3.Checked && !checkBox4.Checked)
            {
                MessageBox.Show("Please select the device that needs to be interlocked");
            }
            else
            {
                if (checkBox1.Checked)
                {
                    doorCheck[0] = 1;
                }
                else
                {
                    doorCheck[0] = 0;
                }
                if (checkBox2.Checked)
                {
                    doorCheck[1] = 1;
                }
                else
                {
                    doorCheck[1] = 0;
                }
                if (checkBox3.Checked)
                {
                    doorCheck[2] = 1;
                }
                else
                {
                    doorCheck[2] = 0;
                }
                if (checkBox4.Checked)
                {
                    doorCheck[3] = 1;
                }
                else
                {
                    doorCheck[3] = 0;
                }
                string[] s = { mac, ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
                IntPtr result = cl2.simpleInterlock(s, doorCheck);
                MessageBox.Show(Convert.ToString(result));
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {

            int[] doorCheck = new int[4];
            doorCheck[0] = doorCheck[1] = doorCheck[2] = doorCheck[3]=0;
            string[] s = { mac, ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            IntPtr result = cl2.simpleInterlock(s, doorCheck);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
