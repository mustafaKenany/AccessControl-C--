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
    public partial class CSetOpenPassword : Form
    {
        public CSetOpenPassword()
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
        private void CSetOpenPassword_Load(object sender, EventArgs e)
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
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string[] openlck = new string [5];
            openlck[0] = "0000";
            if(checkBox2.Checked)
            {
                openlck[4] =  "1";
            }
            else
            {
                openlck[4] = "0";
            }
            if(checkBox3.Checked)
            {
                openlck[3] = "1";
            }
            else
            {
                openlck[3] = "0";
            }
            if(checkBox4.Checked)
            {
                openlck[2] = "1";
            }
            else
            {
                openlck[2] = "0";
            }
            if(checkBox5.Checked)
            {
                openlck[1] = "1";
            }
            else
            {
                openlck[1] = "0";
            }
            string opeLock = openlck[0] + openlck[1] + openlck[2] + openlck[3] + openlck[4];
            string[] s = { mac, ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", textBox1.Text, null, Convert.ToString(doorCount) };
            IntPtr result = cl2.setOpenDoorPwd(s, opeLock);//The order of uploading passwords is reversed
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
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
            this.Close();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            string[] openlck = new string[5];
            openlck[0] = "0000";
            if (checkBox2.Checked)
            {
                openlck[4] = "1";
            }
            else
            {
                openlck[4] = "0";
            }
            if (checkBox3.Checked)
            {
                openlck[3] = "1";
            }
            else
            {
                openlck[3] = "0";
            }
            if (checkBox4.Checked)
            {
                openlck[2] = "1";
            }
            else
            {
                openlck[2] = "0";
            }
            if (checkBox5.Checked)
            {
                openlck[1] = "1";
            }
            else
            {
                openlck[1] = "0";
            }
            string opeLock = openlck[0] + openlck[1] + openlck[2] + openlck[3] + openlck[4];
            string[] s = { mac, ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", textBox1.Text, null, Convert.ToString(doorCount) };
            IntPtr result = cl2.readOpenDoorPwd(s, opeLock);//The order of uploading passwords is reversed
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string[] s = { mac, ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", mode, tcpport, udpport, "0.0.0.0", "", "9010", textBox1.Text, null, Convert.ToString(doorCount) };
            IntPtr result = cl2.clearAllDoorPwd(s);//Clear the door code
        }
    }
}
