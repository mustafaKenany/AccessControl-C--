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
    public partial class CSetAntiSneakBack : Form
    {
        public CSetAntiSneakBack()
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
        int zt = 0;
        private void CSetAntiSneakBack_Load(object sender, EventArgs e)
        {
            if (doorCount == 1)
            {
                checkBox2.Enabled = true;
                checkBox3.Enabled = false;
                checkBox4.Enabled = false;
                checkBox5.Enabled = false;
            }
            else if (doorCount == 2)
            {
                checkBox2.Enabled = true;
                checkBox3.Enabled = true;
                checkBox4.Enabled = false;
                checkBox5.Enabled = false;
            }
            else
            {
                checkBox2.Enabled = true;
                checkBox3.Enabled = true;
                checkBox4.Enabled = true;
                checkBox5.Enabled = true;
            }
            radioButton1.Checked = true;
            zt = 1;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (radioButton1.Checked)
            {
                zt = 1;
            }
            else if (radioButton2.Checked)
            {
                zt = 2;
            }
            string []res = new string[4];
            if (checkBox2.Checked)
            {
                res[0] = "1";
            }
            else
            {
                res[0] = "0";
            }
            if (checkBox3.Checked)
            {
                res[1] = "1";
            }
            else
            {
                res[1] = "0";
            }
            if (checkBox4.Checked)
            {
                res[2] = "1";
            }
            else
            {
                res[2] = "0";
            }
            if (checkBox5.Checked)
            {
                res[3] = "1";
            }
            else
            {
                res[3] = "0";
            }
            string[] s = { mac, ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", null, null, Convert.ToString(doorCount) };
            IntPtr result = cl2.setAntiSneakBack(zt, s, res[0] + res[1] + res[2] + res[3]);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
