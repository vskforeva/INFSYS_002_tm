using System.Diagnostics;
using System.Drawing.Text;
using System.Management;
using Microsoft.VisualBasic;
using LibreHardwareMonitor.Hardware;
using System.Threading;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.DataVisualization.Charting;
using System.Diagnostics;
using System.Management;

namespace INFSYS_002_tm
{
    public partial class Form1 : Form
    {
        private List<Process> processes = null;

        private ListViewItemComparer comparer = null;

        private string tmpInfo = string.Empty;

        private Chart cpuChart;
        private Chart ramChart;
        private Chart diskChart;

        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;

        private System.Windows.Forms.Timer timer1;
        private float totalMemoryMB;

        public Form1()
        {
            InitializeComponent();
        }

        private void GetProcesses()
        {
            processes.Clear();

            processes = Process.GetProcesses().ToList<Process>();
        }

        private void InitializeCharts()
        {
            // Настройка chart1 для CPU
            chart1.ChartAreas.Clear();
            ChartArea cpuArea = new ChartArea("CPU");
            chart1.ChartAreas.Add(cpuArea);

            chart1.Series.Clear();
            Series cpuSeries = new Series("CPU Usage");
            cpuSeries.ChartArea = "CPU";
            cpuSeries.ChartType = SeriesChartType.Line;
            cpuSeries.Legend = "LegendCPU";  // Указываем имя легенды
            chart1.Series.Add(cpuSeries);

            chart1.Legends.Clear();
            chart1.Legends.Add(new Legend("LegendCPU"));

            cpuArea.AxisY.Minimum = 0;
            cpuArea.AxisY.Maximum = 100;

            // Настройка chart2 для RAM
            chart2.ChartAreas.Clear();
            ChartArea ramArea = new ChartArea("RAM");
            chart2.ChartAreas.Add(ramArea);

            chart2.Series.Clear();
            Series ramSeries = new Series("RAM Usage");
            ramSeries.ChartArea = "RAM";
            ramSeries.ChartType = SeriesChartType.Line;
            ramSeries.Legend = "LegendRAM";  // Указываем имя легенды
            chart2.Series.Add(ramSeries);

            chart2.Legends.Clear();
            chart2.Legends.Add(new Legend("LegendRAM"));

            ramArea.AxisY.Minimum = 0;
            ramArea.AxisY.Maximum = 100;

            // Настройка chart3 для диска
            chart3.ChartAreas.Clear();
            ChartArea diskArea = new ChartArea("Disk");
            chart3.ChartAreas.Add(diskArea);

            chart3.Series.Clear();
            Series diskSeries = new Series("Disk Usage");
            diskSeries.ChartArea = "Disk";
            diskSeries.ChartType = SeriesChartType.Line;
            diskSeries.Legend = "LegendDisk";  // Указываем имя легенды
            chart3.Series.Add(diskSeries);

            chart3.Legends.Clear();
            chart3.Legends.Add(new Legend("LegendDisk"));

            diskArea.AxisY.Minimum = 0;
            diskArea.AxisY.Maximum = 100;

            // Инициализация счетчиков производительности
            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            totalMemoryMB = GetTotalMemoryInMB();
        }

        private void InitializeMonitoring()
        {
            totalMemoryMB = GetTotalMemoryInMB();

            timer1 = new System.Windows.Forms.Timer();
            timer1.Interval = 1000;
            timer1.Tick += Timer1_Tick;
            timer1.Start();
        }

        private float GetTotalMemoryInMB()
        {
            ObjectQuery wql = new ObjectQuery("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(wql);
            foreach (ManagementObject result in searcher.Get())
            {
                return Convert.ToSingle(result["TotalVisibleMemorySize"]) / 1024; // в МБ
            }
            return 0;
        }
        private int timeCounter = 0;
        private void Timer1_Tick(object sender, EventArgs e)
        {
            float cpuUsage = cpuCounter.NextValue();
            float availableRAM = ramCounter.NextValue();
            float usedRAM = totalMemoryMB - availableRAM;
            float ramUsagePercent = (usedRAM / totalMemoryMB) * 100;
            float diskUsagePercent = GetDiskUsagePercent("C");

            AddDataPoint(chart1.Series["CPU Usage"], timeCounter, cpuUsage);
            AddDataPoint(chart2.Series["RAM Usage"], timeCounter, ramUsagePercent);
            AddDataPoint(chart3.Series["Disk Usage"], timeCounter, diskUsagePercent);

            timeCounter++;

            chart1.Invalidate();
            chart2.Invalidate();
            chart3.Invalidate();
        }
        private void AddDataPoint(Series series, int x, float y)
        {
            if (series.Points.Count > 60)
                series.Points.RemoveAt(0);

            series.Points.AddXY(x, y);
        }

        private float GetDiskUsagePercent(string driveLetter)
        {
            try
            {
                DriveInfo drive = new DriveInfo(driveLetter);
                if (drive.IsReady)
                {
                    long usedSpace = drive.TotalSize - drive.TotalFreeSpace;
                    return (float)usedSpace / drive.TotalSize * 100;
                }
            }
            catch { }
            return 0;
        }

        private void AddDataPoint(Series series, float value)
        {
            if (series.Points.Count > 60) // Хранить последние 60 точек (примерно 1 минута)
            {
                series.Points.RemoveAt(0);
            }
            series.Points.AddY(value);
        }

        private void RefreshProcessesList(List<Process> processes, string keyword)
        {
            try
            {
                listView1.Items.Clear();

                double memSize = 0;

                foreach (Process p in processes)
                {
                    if (p != null)
                    {
                        memSize = 0;

                        PerformanceCounter pc = new PerformanceCounter();
                        pc.CategoryName = "Process";
                        pc.CounterName = "Working Set - Private";
                        pc.InstanceName = p.ProcessName;

                        memSize = (double)pc.NextValue() / (1000 * 1000);

                        string[] row = new string[] { p.ProcessName.ToString(), Math.Round(memSize, 1).ToString() };

                        listView1.Items.Add(new ListViewItem(row));

                        pc.Close();
                        pc.Dispose();
                    }
                }

                Text = $"Запущено процессов {keyword}: " + processes.Count.ToString();
            }
            catch (Exception) { }
        }

        private void KillProcess(Process process)
        {
            process.Kill();

            process.WaitForExit();
        }

        private void KillProcessAndChildren(int pid)
        {
            if (pid == 0)
            {
                return;
            }

            ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "Selecte * From Win32_Process Where ParentProcessID=" + pid);

            ManagementObjectCollection objectCollection = searcher.Get();

            foreach (ManagementObject obj in objectCollection)
            {
                KillProcessAndChildren(Convert.ToInt32(obj["ProcessID"]));
            }

            try
            {
                Process p = Process.GetProcessById(pid);

                p.Kill();

                p.WaitForExit();
            }
            catch (Exception ex) { }
        }

        private int GetParentProcessId(Process p)
        {
            int parentID = 0;

            try
            {
                ManagementObject managementObject = new ManagementObject("win32_process.handle='" + p.Id + "'");

                managementObject.Get();

                parentID = Convert.ToInt32(managementObject["ParentProcessId"]);
            }
            catch (Exception) { }

            return parentID;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            toolStripComboBox1.SelectedIndex = 0;

            processes = new List<Process>();

            GetProcesses();

            RefreshProcessesList(processes, "");

            comparer = new ListViewItemComparer();
            comparer.ColumnIndex = 0;

            backgroundWorker1.RunWorkerAsync();

        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            GetProcesses();

            RefreshProcessesList(processes, "");
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            try
            {
                if (listView1.SelectedItems[0] != null)
                {
                    Process processToKill = processes.Where((x) => x.ProcessName ==
                    listView1.SelectedItems[0].SubItems[0].Text).ToList()[0];

                    KillProcess(processToKill);

                    GetProcesses();

                    RefreshProcessesList(processes, "");
                }
            }
            catch (Exception) { }
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            try
            {
                if (listView1.SelectedItems[0] != null)
                {
                    Process processToKill = processes.Where((x) => x.ProcessName ==
                    listView1.SelectedItems[0].SubItems[0].Text).ToList()[0];

                    KillProcessAndChildren(GetParentProcessId(processToKill));

                    GetProcesses();

                    RefreshProcessesList(processes, "");
                }
            }
            catch (Exception) { }
        }

        private void завершитьДеревоПроцессовToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (listView1.SelectedItems[0] != null)
                {
                    Process processToKill = processes.Where((x) => x.ProcessName ==
                    listView1.SelectedItems[0].SubItems[0].Text).ToList()[0];

                    KillProcessAndChildren(GetParentProcessId(processToKill));

                    GetProcesses();

                    RefreshProcessesList(processes, "");
                }
            }
            catch (Exception) { }
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string path = Interaction.InputBox("Введите имя программы", "Запуск новой задачи");

            try
            {
                Process.Start(path);
            }
            catch (Exception) { }
        }

        private void toolStripTextBox1_TextChanged(object sender, EventArgs e)
        {
            GetProcesses();

            List<Process> filteredprocesses = processes.Where((x) =>
            x.ProcessName.ToLower().Contains(toolStripTextBox1.Text.ToLower())).ToList<Process>();

            RefreshProcessesList(filteredprocesses, toolStripTextBox1.Text);
        }

        private void listView1_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            comparer.ColumnIndex = e.Column;

            comparer.SortDirection = comparer.SortDirection == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;

            listView1.ListViewItemSorter = comparer;

            listView1.Sort();
        }

        private void выходToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        /// 
        /// 
        /// 
        /// 
        /// 

        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string key = string.Empty;

            switch (toolStripComboBox1.SelectedItem.ToString())
            {
                case "Процессор":
                    key = "Win32_Processor";
                    break;
                case "Видеокарта":
                    key = "Win32_VideoController";
                    break;
                case "Чипсет":
                    key = "Win32_IDEController";
                    break;
                case "Батарея":
                    key = "Win32_Battery";
                    break;
                case "Биос":
                    key = "Win32_BIOS";
                    break;
                case "Оперативная память":
                    key = "Win32_PhysicalMemory";
                    break;
                case "Кэш":
                    key = "Win32_CacheMemory";
                    break;
                case "USB":
                    key = "Win32_USBController";
                    break;
                case "Диск":
                    key = "Win32_DiskDrive";
                    break;
                case "Логические диски":
                    key = "Win32_LogicalDisk";
                    break;
                case "Клавиатура":
                    key = "Win32_Keyboard";
                    break;
                case "Сеть":
                    key = "Win32_NetworkAdapter";
                    break;
                case "Пользователи":
                    key = "Win32_Account";
                    break;
                default:
                    key = "Win32_Processor";
                    break;
            }

            GetHardWareInfo(key, listView2);
        }

        private void GetHardWareInfo(string key, ListView list)
        {
            list.Items.Clear();

            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM " + key);

            try
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    ListViewGroup listViewGroup;

                    try
                    {
                        listViewGroup = list.Groups.Add(obj["Name"].ToString(), obj["Name"].ToString());
                    }
                    catch (Exception ex)
                    {
                        listViewGroup = list.Groups.Add(obj.ToString(), obj.ToString());
                    }

                    if (obj.Properties.Count == 0)
                    {
                        MessageBox.Show("Не удалось получить информацию", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        return;
                    }

                    foreach (PropertyData data in obj.Properties)
                    {
                        ListViewItem item = new ListViewItem(listViewGroup);

                        if (list.Items.Count % 2 != 0)
                        {
                            item.BackColor = Color.White;
                        }
                        else
                        {
                            item.BackColor = Color.WhiteSmoke;
                        }

                        item.Text = data.Name;

                        if (data.Value != null && !string.IsNullOrEmpty(data.Value.ToString()))
                        {
                            switch (data.Value.GetType().ToString())
                            {
                                case "System.String[]":

                                    string[] stringData = data.Value as string[];

                                    string resStr1 = string.Empty;

                                    foreach (string s in stringData)
                                    {
                                        resStr1 += $"{s} ";
                                    }

                                    item.SubItems.Add(resStr1);

                                    break;
                                case "System.UInt16[]":

                                    ushort[] ushortData = data.Value as ushort[];

                                    string resStr2 = string.Empty;

                                    foreach (ushort u in ushortData)
                                    {
                                        resStr2 += $"{Convert.ToString(u)} ";
                                    }

                                    item.SubItems.Add(resStr2);

                                    break;
                                default:

                                    item.SubItems.Add(data.Value.ToString());

                                    break;
                            }

                            list.Items.Add(item);

                        }

                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
        }
        ///
        ///
        ///
        ///
        ///
        private void GetCPUTemperature()
        {
            tmpInfo = string.Empty;

            Visitor visitor = new Visitor();

            Computer computer = new Computer();
            computer.Open();
            computer.IsCpuEnabled = true;
            computer.Accept((IVisitor)visitor);

            for (int i = 0; i < computer.Hardware.Count; i++)
            {
                if (computer.Hardware[i].HardwareType == HardwareType.Cpu)
                {
                    for (int j = 0; j < computer.Hardware[i].Sensors.Length; j++)
                    {
                        if (computer.Hardware[i].Sensors[j].SensorType == SensorType.Temperature)
                        {
                            tmpInfo += computer.Hardware[i].Sensors[j].Name + ": " +
                                computer.Hardware[i].Sensors[j].Value.ToString() + "\n ";
                        }
                    }
                }
            }
            richTextBox1.Text = tmpInfo;

            computer.Close();
        }

        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            while (true)
            {
                GetCPUTemperature();

                Thread.Sleep(100);
            }
        }

        private bool chartsInitialized = false;

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab == tabPage3 && !chartsInitialized)
            {
                InitializeCharts();
                InitializeMonitoring();
                chartsInitialized = true;
            }
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void chart1_Click(object sender, EventArgs e)
        {

        }
        private System.Windows.Forms.Timer testTimer;
        private int counter = 0;

        private void button1_Click(object sender, EventArgs e)
        {
            testTimer = new System.Windows.Forms.Timer();
            testTimer.Interval = 1000;
            testTimer.Tick += (s, ev) =>
            {
                Debug.WriteLine($"Test timer tick {counter++}");
            };
            testTimer.Start();
        }

        private void chart1_Click_1(object sender, EventArgs e)
        {

        }
    }
}
