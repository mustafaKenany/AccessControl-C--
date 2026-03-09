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
    public partial class CDlgGrant : Form
    {
        public CDlgGrant()
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
        public string cardPassword;
        private void CDlgGrant_Load(object sender, EventArgs e)
        {
            dateTimePicker1.CustomFormat = "yyyy-MM-dd";
            dateTimePicker2.CustomFormat = "HH:mm:ss";

            comboBox4.Items.Add("Invalidate immediately");
            for (int i = 0; i < 1000; i++)
            {
                comboBox4.Items.Add(i+1);
            }
            comboBox4.Items.Add("Unlimited times");
            for (int i = 0; i < 64; i++)
            {
                comboBox3.Items.Add(Convert.ToString(i+1));
            }
            comboBox1.Items.Add("Ordinary door opening card");
            comboBox1.Items.Add("First Card Privilege Card");
            comboBox1.Items.Add("Always open privilege card");
            comboBox1.Items.Add("patrol check-in card");
            comboBox1.Items.Add("Anti-theft setting card");
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
            if (textBox1.Text == "")
            {
                MessageBox.Show("Card number cannot be empty");
                return;
            }
            if (textBox2.Text!="")
            {
                if (textBox2.Text.Length < 4 || textBox2.Text.Length > 8)
                {
                    MessageBox.Show("The password length is between 4 digits and 8 digits!");
                    return;
                }
            }
            if (comboBox1.Text == "" )
            {
                MessageBox.Show("Please choose card mode!");
                return;
            }
            if (comboBox3.Text == "" || comboBox4.Text == "")
            {
                MessageBox.Show("The opening time and the number of openings cannot be empty!");
                return;
            }
            string selectdoor="";
            if(checkBox2.Checked)
            {
                selectdoor = selectdoor + "01";
            }
            else
            {
                selectdoor = selectdoor + "00";
            }
            if(checkBox3.Checked)
            {
                selectdoor = selectdoor + "01";
            }
            else
            {
                selectdoor = selectdoor + "00";
            }
            if (checkBox4.Checked)
            {
                selectdoor = selectdoor + "01";
            }
            else
            {
                selectdoor = selectdoor + "00";
            }
            if (checkBox5.Checked)
            {
                selectdoor = selectdoor + "01";
            }
            else
            {
                selectdoor = selectdoor + "00";
            }
            IntPtr ioFlag = new IntPtr(0003);
            int Effectivetimes = 0;//Effective times
            if (comboBox4.Text=="Invalidate immediately")
            {
                Effectivetimes = 0;
            }
            else if (comboBox4.Text == "Unlimited times")
            {
                Effectivetimes = 65535;
            }
            else
            {
                Effectivetimes = comboBox4.SelectedIndex;
            }
            int cardType = 0;//Card category
            cardType = comboBox1.SelectedIndex;
            string validTime = dateTimePicker1.Text + " " + dateTimePicker2.Text;
            int holidayPermission=0;
            if (checkBox1.Checked)
            {
                holidayPermission = 1;
            }
            //Choose opening time
            string openingTime="";
            openingTime = comboBox3.Text;
            //Set door password
            cardPassword = textBox2.Text;
            IntPtr result = cl2.addUnSortCard(sn, ip, Convert.ToInt16(tcpport), password, 1, textBox1.Text, cardPassword, cardType, ioFlag, Effectivetimes, selectdoor, validTime, openingTime, holidayPermission);
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
            this.Close();
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
    }
}
