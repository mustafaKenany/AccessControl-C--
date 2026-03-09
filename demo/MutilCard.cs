using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace demo
{
    public partial class MutilCard : Form
    {
        public MutilCard()
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

        //Define a string array to store the card information of Group A and Group B
        string[] ACardInfo = new string[250]; //Group A card data
        string[] BCardInfo = new string[2000];//Group B card data
        string[] GCardInfo = new string[80];//Fixed card data
        private void button3_Click(object sender, EventArgs e)
        {
            int ww = tabControl1.SelectedIndex;

            string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };
            string result = "", resultAll = "";
            if (checkBox1.Checked)
            {
                result = cl2.getGroupModeInfo(s, 1, "");
                resultAll = resultAll + "1:";
                if (result.Substring(0, 2) == "01")
                {
                    resultAll = resultAll + "Multi-card mode: AB group combination;" + "Number of swiping cards in Group A:" + result.Substring(2, 2) + " Number of swiping cards in Group B:" + result.Substring(4, 2);
                }
                else if (result.Substring(0, 2) == "02")
                {
                    resultAll = resultAll + "Multi-card mode: fixed combination;" + "Number of swiping cards:" + result.Substring(2, 2);
                }
                else if (result.Substring(0, 2) == "00")
                {
                    resultAll = resultAll + "Multi-card mode: Free combination;" + "Number of swiping cards:" + result.Substring(2, 2);
                }
                resultAll = resultAll + "\r\n";
            }

            if (checkBox2.Checked)
            {
                result = cl2.getGroupModeInfo(s, 2, "");
                resultAll = resultAll + "2:";
                if (result.Substring(0, 2) == "01")
                {
                    resultAll = resultAll + "Multi-card mode: AB group combination;" + "Number of swiping cards in Group A:" + result.Substring(2, 2) + " Number of swiping cards in Group B:" + result.Substring(4, 2);
                }
                else if (result.Substring(0, 2) == "02")
                {
                    resultAll = resultAll + "Multi-card mode: fixed combination;" + "Number of swiping cards:" + result.Substring(2, 2);
                }
                else if (result.Substring(0, 2) == "00")
                {
                    resultAll = resultAll + "Multi-card mode: Free combination;" + "Number of swiping cards:" + result.Substring(2, 2);
                }
                resultAll = resultAll + "\r\n";
            }

            if (checkBox3.Checked)
            {
                result = cl2.getGroupModeInfo(s, 3, "");
                resultAll = resultAll + "3:";
                if (result.Substring(0, 2) == "01")
                {
                    resultAll = resultAll + "Multi-card mode: AB group combination;" + "Number of swiping cards in Group A:" + result.Substring(2, 2) + " Number of swiping cards in Group B:" + result.Substring(4, 2);
                }
                else if (result.Substring(0, 2) == "02")
                {
                    resultAll = resultAll + "Multi-card mode: fixed combination;" + "Number of swiping cards:" + result.Substring(2, 2);
                }
                else if (result.Substring(0, 2) == "00")
                {
                    resultAll = resultAll + "Multi-card mode: Free combination;" + "Number of swiping cards:" + result.Substring(2, 2);
                }
                resultAll = resultAll + "\r\n";
            }

            if (checkBox4.Checked)
            {
                result = cl2.getGroupModeInfo(s, 4, "");
                resultAll = resultAll + "4:";
                if (result.Substring(0, 2) == "01")
                {
                    resultAll = resultAll + "Multi-card mode: AB group combination;" + "Number of swiping cards in Group A:" + result.Substring(2, 2) + " Number of swiping cards in Group B:" + result.Substring(4, 2);
                }
                else if (result.Substring(0, 2) == "02")
                {
                    resultAll = resultAll + "Multi-card mode: fixed combination;" + "Number of swiping cards:" + result.Substring(2, 2);
                }
                else if (result.Substring(0, 2) == "00")
                {
                    resultAll = resultAll + "Multi-card mode: Free combination;" + "Number of swiping cards:" + result.Substring(2, 2);
                }
                resultAll = resultAll + "\r\n";
            }
            if (resultAll != null)
            {
                MessageBox.Show(resultAll);
            }
        }

        private void MutilCard_Load(object sender, EventArgs e)
        {
            //初始化控件
            comboBox3.Items.Add("Group A");
            comboBox3.Items.Add("Group B");


            //Group A最多支持5个Group No，Group B20个
            comboBox4.Items.Add("1");
            comboBox4.Items.Add("2");
            comboBox4.Items.Add("3");
            comboBox4.Items.Add("4");
            comboBox4.Items.Add("5");

            //固定组合最多10个Group No
            comboBox5.Items.Add("1");
            comboBox5.Items.Add("2");
            comboBox5.Items.Add("3");
            comboBox5.Items.Add("4");
            comboBox5.Items.Add("5");
            comboBox5.Items.Add("6");
            comboBox5.Items.Add("7");
            comboBox5.Items.Add("8");
            comboBox5.Items.Add("9");
            comboBox5.Items.Add("10");

            comboBox6.Items.Add("Entry multi-card verification");
            comboBox6.Items.Add("Exit multi-card verification");
            comboBox6.Items.Add("Entry and exit multi-card verification");

            dataGridView1.AllowUserToAddRows = false;
            dataGridView2.AllowUserToAddRows = false;

            dataGridView1.ColumnCount = 2;
            dataGridView1.ColumnHeadersVisible = true;
            dataGridView2.ColumnCount = 2;
            dataGridView2.ColumnHeadersVisible = true;

            // Set the column header style.
            DataGridViewCellStyle columnHeaderStyle = new DataGridViewCellStyle();

            columnHeaderStyle.BackColor = Color.Beige;
            columnHeaderStyle.Font = new Font("Arial", 10, FontStyle.Bold);
            dataGridView1.ColumnHeadersDefaultCellStyle = columnHeaderStyle;
            dataGridView2.ColumnHeadersDefaultCellStyle = columnHeaderStyle;

            // Set the column header names.
            dataGridView1.Columns[0].Name = "Serial number";
            dataGridView1.Columns[1].Name = "Card";
            dataGridView1.Columns[1].Width = dataGridView1.Width - dataGridView1.Columns[1].Width;

            dataGridView2.Columns[0].Name = "Serial number";
            dataGridView2.Columns[1].Name = "Card";
            dataGridView2.Columns[1].Width = dataGridView2.Width - dataGridView2.Columns[1].Width;

            for (int i = 0; i < 8; i++)
            {
                //The added line is the first line
                int index = this.dataGridView2.Rows.Add();
                dataGridView2.Rows[index].Cells[0].Value = i + 1;
                dataGridView2.Rows[index].Cells[1].Value = "";
            }
        }
        private void dataGridView1_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            //Set the width for each column. This will only be set once, not once for each execution line
            foreach (DataGridViewColumn c in dataGridView1.Columns)
            {
                c.Width = 100;
            }
        }
        //Define the number of group numbers according to the group type
        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBox4.Items.Clear();
            comboBox4.Text = "";
            dataGridView1.Rows.Clear();
            string path = "";
            if(comboBox3.SelectedIndex==0)
            {
                comboBox4.Items.Add("1");
                comboBox4.Items.Add("2");
                comboBox4.Items.Add("3");
                comboBox4.Items.Add("4");
                comboBox4.Items.Add("5");

                DataGridViewRow dr = new DataGridViewRow();
                dr.CreateCells(dataGridView1);

                for (int i = 0; i < 50; i++)
                {
                    //The added line is the first line
                    int index = this.dataGridView1.Rows.Add();
                    dataGridView1.Rows[index].Cells[0].Value = i + 1;
                    dataGridView1.Rows[index].Cells[1].Value = "";
                }


            }
            else if (comboBox3.SelectedIndex == 1)
            {
                comboBox4.Items.Add("1");
                comboBox4.Items.Add("2");
                comboBox4.Items.Add("3");
                comboBox4.Items.Add("4");
                comboBox4.Items.Add("5");
                comboBox4.Items.Add("6");
                comboBox4.Items.Add("7");
                comboBox4.Items.Add("8");
                comboBox4.Items.Add("9");
                comboBox4.Items.Add("10");
                comboBox4.Items.Add("11");
                comboBox4.Items.Add("12");
                comboBox4.Items.Add("13");
                comboBox4.Items.Add("14");
                comboBox4.Items.Add("15");
                comboBox4.Items.Add("16");
                comboBox4.Items.Add("17");
                comboBox4.Items.Add("18");
                comboBox4.Items.Add("19");
                comboBox4.Items.Add("20");

                DataGridViewRow dr = new DataGridViewRow();
                dr.CreateCells(dataGridView1);

                for (int i = 0; i < 100; i++)
                {
                    //The added line is the first line
                    int index = this.dataGridView1.Rows.Add();
                    dataGridView1.Rows[index].Cells[0].Value = i + 1;
                    dataGridView1.Rows[index].Cells[1].Value = "";
                }
            }
            comboBox4.Text = "1";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex < 0 || comboBox2.SelectedIndex < 0)
            {
                MessageBox.Show("Please select verification mode and anti-passback detection method");
                return;
            }

            string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };

            if (checkBox1.Checked)
            {
                int result = cl2.MultiCardVerifyMode(s, 1, comboBox1.SelectedIndex, comboBox2.SelectedIndex);
            }

            if (checkBox2.Checked)
            {
                int result = cl2.MultiCardVerifyMode(s, 2, comboBox1.SelectedIndex, comboBox2.SelectedIndex);
            }

            if (checkBox3.Checked)
            {
                int result = cl2.MultiCardVerifyMode(s, 3, comboBox1.SelectedIndex, comboBox2.SelectedIndex);
            }

            if (checkBox4.Checked)
            {
                int result = cl2.MultiCardVerifyMode(s, 4, comboBox1.SelectedIndex, comboBox2.SelectedIndex);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };

            if (tabControl1.SelectedIndex == 0)//Set up AB combination verification
            {
                if (checkBox5.Checked)//EnableDisable
                {
                    //Verify before uploading
                    if (checkBox1.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 1, 1, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                    }

                    if (checkBox2.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 2, 1, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                    }

                    if (checkBox3.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 3, 1, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                    }

                    if (checkBox4.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 4, 1, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                    }
                    //First convert all card information of Group A into hexadecimal, and then upload the card information of Group A to the controller

                    for (int i = 0; i < 5; i++)
                    {
                        //ACardInfo
                        string cardinfo = "";
                        int cardCount = 0;//Count the Number of cards in this group
                        for (int j = 0; j < 50; j++)
                        {
                            if (ACardInfo[i * 50 + j] != null)
                            {
                                cardinfo = cardinfo + Convert.ToString(Convert.ToInt32(ACardInfo[i * 50 + j]), 16).PadLeft(18, '0');
                                cardCount++;
                            }
                        }
                        //Upload the data of Group A corresponding to Group No
                        int result = cl2.ABOpenMode(s, 0, i + 1, cardCount, cardinfo);
                    }
                    //Convert all card information of Group B to hexadecimal, and then upload the card information of Group B to the controller

                    for (int i = 0; i < 20; i++)
                    {
                        //ACardInfo
                        string cardinfo = "";
                        int cardCount = 0;//Count the Number of cards in this group
                        for (int j = 0; j < 100; j++)
                        {
                            if (BCardInfo[i * 100 + j] != null)
                            {
                                cardinfo = cardinfo + Convert.ToString(Convert.ToInt32(BCardInfo[i * 100 + j]), 16).PadLeft(18, '0');
                                cardCount++;
                            }
                        }
                        //Upload the data of Group B corresponding to Group No
                        int result = cl2.ABOpenMode(s, 1, i + 1, cardCount, cardinfo);

                    }

                }
                else
                {
                    //Verify before uploading
                    if (checkBox1.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 1, 0, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                    }

                    if (checkBox2.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 2, 0, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                    }

                    if (checkBox3.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 3, 0, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                    }

                    if (checkBox4.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 4, 0, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                    }
                }
            }
            else if (tabControl1.SelectedIndex == 1)//Set up fixed group verification
            {
                if (comboBox6.SelectedIndex < 0)
                {
                    MessageBox.Show("Please select a verification mode");
                    return;
                }
                if (checkBox5.Checked)//EnableDisable
                {
                    if (checkBox1.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 1, 2, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                        for (int i = 0; i < 10; i++)
                        {
                            //GCardInfo
                            int cardCount = 0;//Count the Number of cards in this group

                            string cardinfo = "";
                            for (int j = 0; j < 8; j++)
                            {
                                if (GCardInfo[i * 8 + j] != null)
                                {
                                    cardinfo = cardinfo + Convert.ToString(Convert.ToInt32(GCardInfo[i * 8 + j]), 16).PadLeft(18, '0');
                                    if (Convert.ToInt32(GCardInfo[i * 8 + j]) > 0)
                                    {
                                        cardCount++;
                                    }
                                }
                            }
                            cardinfo = cardinfo.PadRight(144, '0');
                            //Verify before uploading
                            int result2 = cl2.FixOpenMode(s, 1, comboBox6.SelectedIndex + 1, i + 1, cardCount, cardinfo);
                        }
                    }
                    if (checkBox2.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 2, 2, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                        for (int i = 0; i < 10; i++)
                        {
                            //GCardInfo
                            int cardCount = 0;//Count the Number of cards in this group

                            string cardinfo = "";
                            for (int j = 0; j < 8; j++)
                            {
                                if (GCardInfo[i * 8 + j] != null)
                                {
                                    cardinfo = cardinfo + Convert.ToString(Convert.ToInt32(GCardInfo[i * 8 + j]), 16).PadLeft(18, '0');
                                    if (Convert.ToInt32(GCardInfo[i * 8 + j]) > 0)
                                    {
                                        cardCount++;
                                    }
                                }
                            }
                            cardinfo = cardinfo.PadRight(144, '0');
                            //Verify before uploading
                            int result2 = cl2.FixOpenMode(s, 2, comboBox6.SelectedIndex + 1, i + 1, cardCount, cardinfo);
                        }
                    }
                    if (checkBox3.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 3, 2, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                        for (int i = 0; i < 10; i++)
                        {
                            //GCardInfo
                            int cardCount = 0;//Count the Number of cards in this group

                            string cardinfo = "";
                            for (int j = 0; j < 8; j++)
                            {
                                if (GCardInfo[i * 8 + j] != null)
                                {
                                    cardinfo = cardinfo + Convert.ToString(Convert.ToInt32(GCardInfo[i * 8 + j]), 16).PadLeft(18, '0');
                                    if (Convert.ToInt32(GCardInfo[i * 8 + j]) > 0)
                                    {
                                        cardCount++;
                                    }
                                }
                            }
                            cardinfo = cardinfo.PadRight(144, '0');
                            //Verify before uploading
                            int result2 = cl2.FixOpenMode(s, 3, comboBox6.SelectedIndex + 1, i + 1, cardCount, cardinfo);
                        }
                    }
                    if (checkBox4.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 4, 2, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                        for (int i = 0; i < 10; i++)
                        {
                            //GCardInfo
                            int cardCount = 0;//Count the Number of cards in this group

                            string cardinfo = "";
                            for (int j = 0; j < 8; j++)
                            {
                                if (GCardInfo[i * 8 + j] != null)
                                {
                                    cardinfo = cardinfo + Convert.ToString(Convert.ToInt32(GCardInfo[i * 8 + j]), 16).PadLeft(18, '0');
                                    if (Convert.ToInt32(GCardInfo[i * 8 + j]) > 0)
                                    {
                                        cardCount++;
                                    }
                                }
                            }
                            cardinfo = cardinfo.PadRight(144, '0');
                            //Verify before uploading
                            int result2 = cl2.FixOpenMode(s, 4, comboBox6.SelectedIndex + 1, i + 1, cardCount, cardinfo);
                        }
                    }

                }
                else
                {
                    //Verify before uploading
                    if (checkBox1.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 1, 0, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                    }

                    if (checkBox2.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 2, 0, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                    }

                    if (checkBox3.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 3, 0, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                    }

                    if (checkBox4.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 4, 0, Convert.ToInt16(textBox1.Text), Convert.ToInt16(textBox2.Text));
                    }
                }
            }
            else if (tabControl1.SelectedIndex == 2)//Set up Free combination verification
            {
                if (checkBox5.Checked)//EnableDisable
                {
                    //Verify before uploading
                    if (checkBox1.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 1, 3, Convert.ToInt16(textBox3.Text), 0);
                    }

                    if (checkBox2.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 2, 3, Convert.ToInt16(textBox3.Text), 0);
                    }

                    if (checkBox3.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 3, 3, Convert.ToInt16(textBox3.Text), 0);
                    }

                    if (checkBox4.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 4, 3, Convert.ToInt16(textBox3.Text), 0);
                    }
                }
                else
                {
                    //Verify before uploading
                    if (checkBox1.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 1, 0, Convert.ToInt16(textBox3.Text), 0);
                    }

                    if (checkBox2.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 2, 0, Convert.ToInt16(textBox3.Text), 0);
                    }

                    if (checkBox3.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 3, 0, Convert.ToInt16(textBox3.Text), 0);
                    }

                    if (checkBox4.Checked)
                    {
                        int result = cl2.MultiCardOpenMode(s, 4, 0, Convert.ToInt16(textBox3.Text), 0);
                    }
                }
            }

        }

        //update list
        private void dataGridView1_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            if (dataGridView1.IsCurrentCellDirty)
            {
                dataGridView1.CommitEdit(DataGridViewDataErrorContexts.Commit);
                int www = dataGridView1.CurrentRow.Index;

                if (comboBox3.Text == "Group A")
                {
                    ACardInfo[(Convert.ToInt16(comboBox4.Text) - 1) * 50 + www] = dataGridView1.Rows[www].Cells[1].Value.ToString();
                }
                else if (comboBox3.Text == "Group B")
                {
                    BCardInfo[(Convert.ToInt16(comboBox4.Text) - 1) * 200 + www] = dataGridView1.Rows[www].Cells[1].Value.ToString();
                }

            }
        }

        private void comboBox4_SelectedIndexChanged(object sender, EventArgs e)
        {
            int www = comboBox4.SelectedIndex;

            if (comboBox3.Text == "Group A")
            {
                for (int i = 0; i < 50; i++)
                {
                    dataGridView1.Rows[i].Cells[1].Value = ACardInfo[(Convert.ToInt16(comboBox4.Text) - 1) * 50 + i];
                    //ACardInfo[(Convert.ToInt16(comboBox4.Text) - 1) * 50 + www] = dataGridView1.Rows[www].Cells[1].Value.ToString();
                }
            }
            else if (comboBox3.Text == "Group B")
            {
                for (int i = 0; i < 50; i++)
                {
                    dataGridView1.Rows[i].Cells[1].Value = BCardInfo[(Convert.ToInt16(comboBox4.Text) - 1) * 50 + i];
                    //ACardInfo[(Convert.ToInt16(comboBox4.Text) - 1) * 50 + www] = dataGridView1.Rows[www].Cells[1].Value.ToString();
                }
            }
        }

        private void comboBox5_SelectedIndexChanged(object sender, EventArgs e)
        {
            int www = comboBox5.SelectedIndex;

            for (int i = 0; i < 8; i++)
            {
                dataGridView2.Rows[i].Cells[1].Value = GCardInfo[(Convert.ToInt16(comboBox5.Text) - 1) * 8 + i];
                //ACardInfo[(Convert.ToInt16(comboBox4.Text) - 1) * 50 + www] = dataGridView1.Rows[www].Cells[1].Value.ToString();
            }
        }

        private void dataGridView2_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {

            dataGridView2.CommitEdit(DataGridViewDataErrorContexts.Commit);
            int www = dataGridView2.CurrentRow.Index;
            GCardInfo[(Convert.ToInt16(comboBox5.Text) - 1) * 8 + www] = dataGridView2.Rows[www].Cells[1].Value.ToString();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string[] s = { "s", ip, tcpport, sn, password, null, null, null, null, null, null, null, null, null, null, null, mac, "255.255.255.0", gateway, "0.0.0.0", "0.0.0.0", "server", tcpport, udpport };
            string result = "", resultAll = "";
            if (checkBox1.Checked)
            {
                result = cl2.MultiGetCardVerifyMode(s, 1, "");
                resultAll = resultAll + "1:";
                if (result.Substring(0, 2) == "01")
                {
                    resultAll = resultAll + "When encountering a wrong combination: Exit immediately";
                }
                else
                {
                    resultAll = resultAll + "When encountering the wrong combination: continue to wait";
                }
                if (result.Substring(2, 2) == "01")
                {
                    resultAll = resultAll + " Anti-passback function: Disable";
                }
                else
                {
                    resultAll = resultAll + " Anti-passback function: Enable";
                }
                resultAll = resultAll + "\r\n";
            }

            if (checkBox2.Checked)
            {
                result = cl2.MultiGetCardVerifyMode(s, 2, "");
                resultAll = resultAll + "2:";
                if (result.Substring(0, 2) == "01")
                {
                    resultAll = resultAll + "When encountering a wrong combination: Exit immediately";
                }
                else
                {
                    resultAll = resultAll + "When encountering the wrong combination: continue to wait";
                }
                if (result.Substring(2, 2) == "01")
                {
                    resultAll = resultAll + " Anti-passback function: Disable";
                }
                else
                {
                    resultAll = resultAll + " Anti-passback function: Enable";
                }
                resultAll = resultAll + "\r\n";
            }

            if (checkBox3.Checked)
            {
                result = cl2.MultiGetCardVerifyMode(s, 3, "");
                resultAll = resultAll + "3:";
                if (result.Substring(0, 2) == "01")
                {
                    resultAll = resultAll + "When encountering a wrong combination: Exit immediately";
                }
                else
                {
                    resultAll = resultAll + "When encountering the wrong combination: continue to wait";
                }
                if (result.Substring(2, 2) == "01")
                {
                    resultAll = resultAll + " Anti-passback function: Disable";
                }
                else
                {
                    resultAll = resultAll + " Anti-passback function: Enable";
                }
                resultAll = resultAll + "\r\n";
            }

            if (checkBox4.Checked)
            {
                result = cl2.MultiGetCardVerifyMode(s, 4, "");
                resultAll = resultAll + "4:";
                if (result.Substring(0, 2) == "01")
                {
                    resultAll = resultAll + "When encountering a wrong combination: Exit immediately";
                }
                else
                {
                    resultAll = resultAll + "When encountering the wrong combination: continue to wait";
                }
                if (result.Substring(2, 2) == "01")
                {
                    resultAll = resultAll + " Anti-passback function: Disable";
                }
                else
                {
                    resultAll = resultAll + " Anti-passback function: Enable";
                }
                resultAll = resultAll + "\r\n";
            }
            if (resultAll != null)
            {
                MessageBox.Show(resultAll);
            }
        }

    }
}
