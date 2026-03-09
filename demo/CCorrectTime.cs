using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace demo
{
    public partial class CCorrectTime : Form
    {
        public CCorrectTime()
        {
            InitializeComponent();
        }
        public string[] temp;
        public string FormTitle;
        private const int WM_COPYDATA = 0x004A;  //Message type 
        private const int WM_READTIME = 100;  //Message type 
        private void CCorrectTime_Load(object sender, EventArgs e)
        {
        }
        private void button1_Click(object sender, EventArgs e)
        {
            FormTitle = this.Text;

            int s =0;

            string info=null;

            textBox1.Text = cl2.readDevNowTime(temp, info);

        }

        private void button2_Click(object sender, EventArgs e)
        {
            FormTitle = this.Text;
            IntPtr result = cl2.calibrationTime(temp);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            FormTitle = this.Text;
            IntPtr result = cl2.calibrationTime(temp);
        }
    }
}
