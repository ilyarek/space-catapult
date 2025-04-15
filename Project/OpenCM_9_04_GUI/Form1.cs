using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Management;
using System.Net.NetworkInformation;

namespace OpenCM_9_04_GUI
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] portnames = SerialPort.GetPortNames();
            comboBox1.Items.AddRange(portnames);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                serialPort1.PortName = comboBox1.Text;
                serialPort1.BaudRate = Convert.ToInt32(comboBox2.Text);

                serialPort1.Open();

                comboBox1.Enabled = false;
                comboBox2.Enabled = false;
                comboBox3.Enabled = true;
                button1.Enabled = false;
                button2.Enabled = true;
                button3.Enabled = true;
                button4.Enabled = true;
                button6.Enabled = true;
                numericUpDown1.Enabled = true;
                numericUpDown2.Enabled = true;
                numericUpDown3.Enabled = true;

                textBox1.Text += "Port " + serialPort1.PortName.ToString() + " is open" + Environment.NewLine;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                serialPort1.Close();

                button1.Enabled = true;
                comboBox1.Enabled = true;
                comboBox2.Enabled = true;
                comboBox3.Enabled = false;
                button2.Enabled = false;
                button3.Enabled = false;
                button4.Enabled = false;
                button6.Enabled = false;
                numericUpDown1.Enabled = false;
                numericUpDown2.Enabled = false;
                numericUpDown3.Enabled = false;

                textBox1.Text += "Port " + serialPort1.PortName.ToString() + " is closed" + Environment.NewLine;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            servo_move((float)numericUpDown1.Value, (float)numericUpDown2.Value, (float)numericUpDown3.Value);
            textBox1.Text += "Moving to " + numericUpDown1.Value + " " + numericUpDown2.Value + " " + numericUpDown3.Value + " " + Environment.NewLine;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            byte[] buffer = new byte[1];
            buffer[0] = (byte)0;
            serialPort1.Write(buffer, 0, buffer.Length);
            textBox1.Text += "Pinging..." + Environment.NewLine;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
            }
        }

        private void servo_ping(byte[] buffer)
        {
            textBox1.Text += "Done!" + Environment.NewLine;
            for (int servo=1; servo<6; servo++)
            {
                if (buffer[servo] == (byte) 1)
                {
                    textBox1.Text += "Servo " + servo + " OK" + Environment.NewLine;
                }
                else
                {
                    textBox1.Text += "Servo " + servo + " not connected" + Environment.NewLine;
                }
            }
        }

        private void servo_move(float rotate, float joint1, float joint2)
        {
            byte[] buffer = new byte[7];
            buffer[0] = (byte)1;
            byte[] joint1_arr = new byte[2];
            byte[] joint2_arr = new byte[2];
            byte[] joint3_arr = new byte[2];
            joint1_arr = BitConverter.GetBytes((Int16)Math.Round((rotate * 1023) / 300));
            joint2_arr = BitConverter.GetBytes((Int16)Math.Round((joint1 * 1023) / 300));
            joint3_arr = BitConverter.GetBytes((Int16)Math.Round((joint2 * 1023) / 300));
            buffer[1] = joint1_arr[0];
            buffer[2] = joint1_arr[1];
            buffer[3] = joint2_arr[0];
            buffer[4] = joint2_arr[1];
            buffer[5] = joint3_arr[0];
            buffer[6] = joint3_arr[1];
            serialPort1.Write(buffer, 0, buffer.Length);

            pictureBox1.Invalidate();
            pictureBox2.Invalidate();
        }

        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            byte[] buffer = new byte[serialPort1.BytesToRead];
            serialPort1.Read(buffer, 0, buffer.Length);
            switch(buffer[0])
            {
                default:
                    textBox1.Text += "Buffer error: " + buffer[0] + Environment.NewLine;
                    break;
                case ((byte) 0):
                    servo_ping(buffer);
                    break;
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            textBox1.SelectionStart = textBox1.Text.Length;
            textBox1.ScrollToCaret();
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.White);

            float baseX = pictureBox1.Width / 2;
            float baseY = pictureBox1.Height / 2;
            float radius1 = 150;

            float rotation_angle = (float)numericUpDown1.Value;

            float x1 = baseX + radius1 * (float)0.4 * (float)Math.Cos((rotation_angle - 60) * Math.PI / 180);
            float y1 = baseY + radius1 * (float)0.4 * (float)Math.Sin((rotation_angle - 60) * Math.PI / 180);
            float x2 = baseX + radius1 * (float)1.1 * (float)Math.Cos((rotation_angle + 120) * Math.PI / 180);
            float y2 = baseY + radius1 * (float)1.1 * (float)Math.Sin((rotation_angle + 120) * Math.PI / 180);

            Pen RotationPen = new Pen(Brushes.Red, 5);

            g.DrawLine(RotationPen, x1, y1, x2, y2);

            for (float i = 120; i < 360; i+=30)
            {
                g.DrawLine(Pens.Black, baseX, baseY, baseX + radius1 * (float) 1.05 * (float)Math.Cos(i * Math.PI / 180), baseY + radius1 * (float) 1.05 * (float)Math.Sin(i * Math.PI / 180));
            }
            for (float i = 0; i < 90; i += 30)
            {
                g.DrawLine(Pens.Black, baseX, baseY, baseX + radius1 * (float) 1.05 *(float)Math.Cos(i * Math.PI / 180), baseY + radius1 * (float) 1.05 * (float)Math.Sin(i * Math.PI / 180));
            }

            float start_angle = 120;
            float sweep_angle = 300;

            g.DrawArc(Pens.Black, baseX - radius1, baseY - radius1, radius1 * 2, radius1 * 2, start_angle, sweep_angle);
            g.DrawArc(Pens.Black, baseX - radius1/2, baseY - radius1/2, radius1, radius1, start_angle, sweep_angle);
        }

        private void pictureBox2_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.White);

            float baseX = pictureBox1.Width / 2;
            float baseY = pictureBox1.Height / 2;
            float radius2 = 90;
            float radius3 = 60;

            g.DrawLine(Pens.Black, baseX - 85, baseY, baseX + 115, baseY);
            g.DrawLine(Pens.Black, baseX, baseY, baseX, baseY - 130);

            float joint1_angle = (float)numericUpDown2.Value;
            float joint2_angle = (float)numericUpDown3.Value;

            float x1 = baseX + radius2 * (float)Math.Cos((joint1_angle + 120) * Math.PI / 180);
            float y1 = baseY + radius2 * (float)Math.Sin((joint1_angle + 120) * Math.PI / 180);
            float x2 = x1 + radius3 * (float)Math.Cos((joint1_angle + joint2_angle - 30) * Math.PI / 180);
            float y2 = y1 + radius3 * (float)Math.Sin((joint1_angle + joint2_angle - 30) * Math.PI / 180);

            Pen Joint1Pen = new Pen(Brushes.Blue, 3);
            Pen Joint2Pen = new Pen(Brushes.Orange, 3);

            g.DrawLine(Joint1Pen, baseX, baseY, x1, y1);
            g.DrawLine(Joint2Pen, x1, y1, x2, y2);

            g.DrawLine(Pens.Black, baseX, baseY, baseX + radius2 * (float)0.55 * (float)Math.Cos(120 * Math.PI / 180), baseY + radius2 * (float)0.55 * (float)Math.Sin(120 * Math.PI / 180));
            g.DrawLine(Pens.Black, baseX, baseY, baseX + radius2 * (float)0.55 * (float)Math.Cos(60 * Math.PI / 180), baseY + radius2 * (float)0.55 * (float)Math.Sin(60 * Math.PI / 180));

            for (float i = 45; i < 360; i += 90)
            {
                g.DrawLine(Pens.Black, baseX, baseY, baseX + radius2 * (float)0.55 * (float)Math.Cos(i * Math.PI / 180), baseY + radius2 * (float)0.55 * (float)Math.Sin(i * Math.PI / 180));
            }

            g.DrawLine(Pens.Black, x1, y1, x1 + radius3 * (float)0.35 * (float)Math.Cos((joint1_angle - 30) * Math.PI / 180), y1 + radius3 * (float)0.35 * (float)Math.Sin((joint1_angle - 30) * Math.PI / 180));
            g.DrawLine(Pens.Black, x1, y1, x1 + radius3 * (float)0.35 * (float)Math.Cos((joint1_angle - 90) * Math.PI / 180), y1 + radius3 * (float)0.35 * (float)Math.Sin((joint1_angle - 90) * Math.PI / 180));

            for (float i = 45; i < 360; i += 90)
            {
                g.DrawLine(Pens.Black, x1, y1, x1 + radius3 * (float)0.35 * (float)Math.Cos((i + joint1_angle - 150) * Math.PI / 180), y1 + radius3 * (float)0.35 * (float)Math.Sin((i + joint1_angle - 150) * Math.PI / 180));
            }

            float start_angle = 120;
            float sweep_angle = 300;

            g.DrawArc(Pens.Black, baseX - radius2 * (float) 0.5, baseY - radius2 * (float) 0.5, radius2 * 2 * (float) 0.5, radius2 * 2 * (float) 0.5, start_angle, sweep_angle);
            g.DrawArc(Pens.Black, x1 - radius3 * (float)0.3, y1 - radius3 * (float)0.3, radius3 * 2 * (float)0.3, radius3 * 2 * (float)0.3, start_angle + joint1_angle - 150, sweep_angle);

            g.DrawString("front", new Font("Microsoft Sans Serif", 10), Brushes.Black, baseX + 50, baseY + 5);
            g.DrawString("back", new Font("Microsoft Sans Serif", 10), Brushes.Black, baseX - 80, baseY + 5);
        }
    }
}
