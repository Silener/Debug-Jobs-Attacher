using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;
using System.Resources;
using System.Reflection;
using System.Globalization;
using System.Windows.Forms;
using System.Drawing;
using System.Collections;
using WindowsApplication1;
using System.Diagnostics;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using DTEProcess = EnvDTE.Process;
using Process = System.Diagnostics.Process;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation",
	Justification = "Reviewed. Suppression is OK here.", Scope = "class")]
public static class VisualStudioAttacher
{
	[DllImport("ole32.dll")]
	public static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

	[DllImport("ole32.dll")]
	public static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	public static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	public static extern IntPtr SetFocus(IntPtr hWnd);

	public static string GetSolutionForVisualStudio(Process visualStudioProcess)
	{
		_DTE visualStudioInstance;
		if (TryGetVsInstance(visualStudioProcess.Id, out visualStudioInstance))
		{
			try
			{
				return visualStudioInstance.Solution.FullName;
			}
			catch (Exception)
			{
			}
		}

		return null;
	}

	/// <summary>
	/// The method to use to attach visual studio to a specified process.
	/// </summary>
	/// <param name="visualStudioProcess">
	/// The visual studio process to attach to.
	/// </param>
	/// <param name="applicationProcess">
	/// The application process that needs to be debugged.
	/// </param>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the application process is null.
	/// </exception>
	public static void AttachVisualStudioToProcess(Process visualStudioProcess, Process applicationProcess)
	{
		_DTE visualStudioInstance;

		if (TryGetVsInstance(visualStudioProcess.Id, out visualStudioInstance))
		{
			// Find the process you want the VS instance to attach to...
			DTEProcess processToAttachTo =
				visualStudioInstance.Debugger.LocalProcesses.Cast<DTEProcess>()
									.FirstOrDefault(process => process.ProcessID == applicationProcess.Id);

			// Attach to the process.
			if (processToAttachTo != null)
			{
				processToAttachTo.Attach();

				ShowWindow((int)visualStudioProcess.MainWindowHandle, 3);
				SetForegroundWindow(visualStudioProcess.MainWindowHandle);
			}
			else
			{
				throw new InvalidOperationException(
					"Visual Studio process cannot find specified application '" + applicationProcess.Id + "'");
			}
		}
	}

	/// <summary>
	/// The get visual studio for solutions.
	/// </summary>
	/// <param name="solutionNames">
	/// The solution names.
	/// </param>
	/// <returns>
	/// The <see cref="Process"/>.
	/// </returns>
	public static Process GetVisualStudioForSolutions(List<string> solutionNames)
	{
		foreach (string solution in solutionNames)
		{
			Process visualStudioForSolution = GetVisualStudioForSolution(solution);
			if (visualStudioForSolution != null)
			{
				return visualStudioForSolution;
			}
		}

		return null;
	}

	/// <summary>
	/// The get visual studio process that is running and has the specified solution loaded.
	/// </summary>
	/// <param name="solutionName">
	/// The solution name to look for.
	/// </param>
	/// <returns>
	/// The visual studio <see cref="Process"/> with the specified solution name.
	/// </returns>
	public static Process GetVisualStudioForSolution(string solutionName)
	{
		IEnumerable<Process> visualStudios = GetVisualStudioProcesses();

		foreach (Process visualStudio in visualStudios)
		{
			_DTE visualStudioInstance;



			if (TryGetVsInstance(visualStudio.Id, out visualStudioInstance))
			{
				try
				{
					string actualSolutionName = Path.GetFileName(visualStudioInstance.Solution.FullName);

					if (string.Compare(
						actualSolutionName,
						solutionName,
						StringComparison.InvariantCultureIgnoreCase) == 0)
					{
						return visualStudio;
					}
				}
				catch (Exception)
				{
					throw;
				}
			}
		}

		return null;
	}

	[DllImport("User32")]
	private static extern int ShowWindow(int hwnd, int nCmdShow);

	private static IEnumerable<Process> GetVisualStudioProcesses()
	{
		Process[] processes = Process.GetProcesses();
		return processes.Where(o => o.ProcessName.Contains("devenv"));
	}

	private static bool TryGetVsInstance(int processId, out _DTE instance)
	{
		IntPtr numFetched = IntPtr.Zero;
		IRunningObjectTable runningObjectTable;
		IEnumMoniker monikerEnumerator;
		IMoniker[] monikers = new IMoniker[1];

		GetRunningObjectTable(0, out runningObjectTable);
		runningObjectTable.EnumRunning(out monikerEnumerator);
		monikerEnumerator.Reset();

		while (monikerEnumerator.Next(1, monikers, numFetched) == 0)
		{
			IBindCtx ctx;
			CreateBindCtx(0, out ctx);

			string runningObjectName;
			monikers[0].GetDisplayName(ctx, null, out runningObjectName);

			object runningObjectVal;
			runningObjectTable.GetObject(monikers[0], out runningObjectVal);

			if (runningObjectVal is _DTE && runningObjectName.StartsWith("!VisualStudio"))
			{
				int currentProcessId = int.Parse(runningObjectName.Split(':')[1]);

				if (currentProcessId == processId)
				{
					instance = (_DTE)runningObjectVal;
					return true;
				}
			}
		}

		instance = null;
		return false;
	}
}

namespace WindowsApplication1
{
	using System;
	using System.Drawing;
	using System.Collections;
	using System.ComponentModel;
	using System.Windows.Forms;
	using System.Data;
	using System.IO;
	using System.Management;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.InteropServices;
	using System.Runtime.InteropServices.ComTypes;
	using System.Text.RegularExpressions;

	public class ProcessesDialog : System.Windows.Forms.Form
	{
		/// <summary>
		/// Import of the kernel for debug check method calls
		/// </summary>
		/// <param name="hProcess"> Process to be checked </param>
		/// <param name="isDebuggerPresent"> Flag, if the process is attached </param>
		/// <returns></returns>
		[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
		static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

		/// <summary>
		///  ListView for the processes
		/// </summary>
		ListView processesView;

		/// <summary>
		/// Attaching button
		/// </summary>
		Button attachButton;

		/// <summary>
		/// 
		/// </summary>
		List<ProcessInfo> processesInfoList;
		Button cancelButton;
		Dictionary<ProcessTypes, string> processTypesDictionary;
		Dictionary<ProcessTypes, string> processInfoDictionary;
		Dictionary<ProcessTypes, string> processNamesDictionary;

		public enum ProcessTypes
		{
			ProcessTypeServerJobFirst,
			ProcessTypeServerJobSecond,
			ProcessTypeServerJobThird,
			ProcessTypeServerJobFourth,
			ProcessTypeServerJobFifth,
			ProcessTypeServerJobSixth,
			ProcessTypeServerJobSeventh,
			ProcessTypeServerJobEigth,
			ProcessTypeServerJobNinth,
			ProcessTypeServerJobTenth,
			ProcessTypeServerJobEleventh,
			ProcessTypeServerJobTwelfth,
			ProcessTypeSdelki,
			ProcessTypeAccessAdmin,
			ProcessTypeCSBAdmin,
			ProcessTypeTouchScreen,
			ProcessTypeKasi
		}

		public ProcessesDialog()
		{
			InitializeComponent();
			this.Size = new Size(400, 350);
		}

		protected override bool ProcessDialogKey(Keys keyData)
		{
			if (Form.ModifierKeys == Keys.None && keyData == Keys.Escape)
			{
				this.Close();
				return true;
			}// if

			return base.ProcessDialogKey(keyData);
		}

		private void Form_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape)
			{
				this.Close();
			}// if
		}

		private bool IsDigitsOnly(string str)
		{
			foreach (char c in str)
			{
				if (c < '0' || c > '9')
					return false;
			}// foreach

			return true;
		}

		private int ProcessWMICResult(string wmicResult, ProcessTypes processType)
		{
			int processID = 0;

			if (wmicResult.Contains(processTypesDictionary[processType]))
			{
				try
				{
					ProcessInfo process = new ProcessInfo();
					string[] fullProcessInfo = wmicResult.Split(' ');

					foreach (string processPiece in fullProcessInfo)
					{
						string newProcessPiece = Regex.Replace(processPiece, " ", "");
						if (newProcessPiece.Length <= 0 || !IsDigitsOnly(newProcessPiece))
							continue;

						process.group = (int)processType;
						process.info = processInfoDictionary[processType];
						process.processName = processNamesDictionary[processType];
						process.processID = Convert.ToInt32(newProcessPiece);
						processesInfoList.Add(process);
						break;
					}// foreach
				}// try
				catch (Exception)
				{
					throw;
				}// catch
			}// if

			return processID;
		}

		private void SortOutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
		{
			// Collect the sort command output.
			if (!String.IsNullOrEmpty(outLine.Data))
			{
				var processTypes = Enum.GetValues(typeof(ProcessTypes));

				foreach (ProcessTypes processType in processTypes)
				{
					if (ProcessWMICResult(outLine.Data, processType) > 0)
						return;
				}// foreach
			}// if
		}

		private void LoadClientApp(ProcessTypes processType)
		{
			try
			{
				var psi = new ProcessStartInfo("wmic");
				string arguments = String.Format("process where \" caption like '%%{0}%%' \" get processid,CommandLine", processTypesDictionary[processType]);
				psi.Arguments = arguments;
				psi.WindowStyle = ProcessWindowStyle.Minimized;
				psi.CreateNoWindow = true;
				psi.UseShellExecute = false;
				psi.RedirectStandardOutput = true;

				var p = System.Diagnostics.Process.Start(psi);
				p.OutputDataReceived += SortOutputHandler;
				p.BeginOutputReadLine();
				p.WaitForExit();
			}// try
			catch (Exception)
			{
				throw;
			}// catch
		}

		private void LoadServerJobs()
		{
			try
			{
				var psi = new ProcessStartInfo("wmic");
				string arguments = String.Format("process where \" caption like '%%VCSBankServerJob%%' \" get processid,CommandLine");
				psi.Arguments = arguments;
				psi.WindowStyle = ProcessWindowStyle.Minimized;
				psi.CreateNoWindow = true;
				psi.UseShellExecute = false;
				psi.RedirectStandardOutput = true;

				var p = System.Diagnostics.Process.Start(psi);
				p.OutputDataReceived += SortOutputHandler;
				p.BeginOutputReadLine();
				p.WaitForExit();
			}// try
			catch (Exception)
			{
				throw;
			}// catch
		}

		void AttachEventHandler(object sender, EventArgs e)
		{
			if (processesView.CheckedItems.Count <= 0)
				return;

			this.Hide();
			this.processesView.Visible = false;

			try
			{
				foreach (ListViewItem item in processesView.CheckedItems)
				{
					Process vsProc = Process.GetCurrentProcess();
					Process jobProc = Process.GetProcessById(Convert.ToInt32(item.Text));
					VisualStudioAttacher.AttachVisualStudioToProcess(vsProc, jobProc);
				}// foreach
			}// try
			catch (Exception)
			{
				throw;
			}// catch

			this.Close();
		}

		void CancelHandler(object sender, EventArgs e)
		{
			Close();
		}

		private void EnterKey(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter)
			{
				AttachEventHandler(sender, e);
			}
		}

		private void ProcessChecked(Object sender, ItemCheckedEventArgs e)
		{
			bool isDebuggerPresent = false;
			try
			{
				IntPtr processHandler = Process.GetProcessById(Convert.ToInt32(e.Item.Text)).Handle;

				if (processHandler == null)
					return;

				CheckRemoteDebuggerPresent(processHandler, ref isDebuggerPresent);
			}// try
			catch (Exception)
			{
				throw;
			}// catch

			if (isDebuggerPresent)
			{
				e.Item.Checked = false;
			}// if
		}

		private void InitControls()
		{
			processesInfoList = new List<ProcessInfo>();
			processesView = new ListView();
			cancelButton = new Button();
			attachButton = new Button();

			attachButton.Text = "Attach";
			attachButton.Location = new System.Drawing.Point(20, 280);
			attachButton.BringToFront();
			attachButton.Click += new System.EventHandler(AttachEventHandler);

			cancelButton.Text = "Cancel";
			cancelButton.Location = new System.Drawing.Point(300, 280);
			cancelButton.BringToFront();
			cancelButton.Click += new System.EventHandler(CancelHandler);

			processesView.KeyUp += EnterKey;
			processesView.Bounds = new Rectangle(new Point(5, 5), new Size(500, 350));
			processesView.ItemChecked += ProcessChecked;
			processesView.View = View.Details;
			processesView.CheckBoxes = true;
			processesView.FullRowSelect = true;
			processesView.GridLines = true;
			processesView.Sorting = SortOrder.None;

			processesView.Columns.Add("ProcessID", 70, HorizontalAlignment.Left);
			processesView.Columns.Add("Name", 130, HorizontalAlignment.Left);
			processesView.Columns.Add("Group", 180, HorizontalAlignment.Left);
			processesView.Location = new System.Drawing.Point(5, 5);
			processesView.Size = new System.Drawing.Size(385, 260);
			processesView.TabIndex = 0;

			this.ControlBox = false;
		}

		private void FillListView()
		{
			// Loads the pointed applications info
			LoadServerJobs();
			LoadClientApp(ProcessTypes.ProcessTypeSdelki);
			LoadClientApp(ProcessTypes.ProcessTypeAccessAdmin);
			LoadClientApp(ProcessTypes.ProcessTypeCSBAdmin);
			LoadClientApp(ProcessTypes.ProcessTypeTouchScreen);
			LoadClientApp(ProcessTypes.ProcessTypeKasi);

			// Sorts the list by group code
			List<ProcessInfo> SortedList = processesInfoList.OrderBy(o => o.group).ToList();

			// Adds to the list view each process. If a process is being debugged it is colored in gray
			// An event handler is attached to the click event of the items and if a debugged item is clicked, it becomes unclicked
			try
			{
				foreach (ProcessInfo process in SortedList)
				{
					bool isDebuggerPresent = false;


					IntPtr processHandler = Process.GetProcessById(process.processID).Handle;

					if (processHandler == null)
						continue;

					// Checks whether the process is being debugged
					CheckRemoteDebuggerPresent(processHandler, ref isDebuggerPresent);


					ListViewItem processItem = new ListViewItem(process.processID.ToString(), 0);

					if (isDebuggerPresent)
					{
						processItem.BackColor = Color.Gray;
					}// if

					processItem.Checked = false;
					processItem.SubItems.Add(process.processName);
					processItem.SubItems.Add(process.info);

					processesView.Items.Add(processItem);
				}// foreach
			}// try
			catch (Exception)
			{
				throw;
			}// catch
		}

		private void InitializeProcessTypesDictonary()
		{
			processTypesDictionary = new Dictionary<ProcessTypes, string>();

			processTypesDictionary.Add(ProcessTypes.ProcessTypeServerJobFirst, ";0;");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeServerJobSecond, ";1;");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeServerJobThird, ";2;");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeServerJobFourth, ";3;");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeServerJobFifth, ";4;");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeServerJobSixth, ";5;");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeServerJobSeventh, ";6;");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeServerJobEigth, ";7;");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeServerJobNinth, ";8;");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeServerJobTenth, ";9;");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeServerJobEleventh, ";10;");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeServerJobTwelfth, ";11;");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeSdelki, "sdelki");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeAccessAdmin, "AccessAdmin");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeCSBAdmin, "CSBAdmin");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeTouchScreen, "TouchScreen");
			processTypesDictionary.Add(ProcessTypes.ProcessTypeKasi, "KasiClnt");	
		}

		private void InitializeProcessInfoDictonary()
		{
			processInfoDictionary = new Dictionary<ProcessTypes, string>();

			processInfoDictionary.Add(ProcessTypes.ProcessTypeServerJobFirst, "0 - Онлайн");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeServerJobSecond, "1 - Справки");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeServerJobThird, "2 - RPC заявки от EBank(WebBank)");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeServerJobFourth, "3 - Разпределени заявки");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeServerJobFifth, "4 - Варира според банката");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeServerJobSixth, "5 - Варира според банката");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeServerJobSeventh, "6 - Варира според банката");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeServerJobEigth, "7 - Варира според банката");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeServerJobNinth, "8 - Варира според банката");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeServerJobTenth, "9 - Варира според банката");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeServerJobEleventh, "10 - Варира според банката");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeServerJobTwelfth, "11 - Варира според банката");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeSdelki, "Sdelki");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeAccessAdmin, "CSAccessAdmin");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeCSBAdmin, "CSBAdmin");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeTouchScreen, "CSTouchScreen");
			processInfoDictionary.Add(ProcessTypes.ProcessTypeKasi, "KasiClnt");
		}
		
		private void InitializeProcessNamesDictonary()
		{
			processNamesDictionary = new Dictionary<ProcessTypes, string>();

			DTE dte = (DTE)GetService(typeof(DTE));
			string solutionDir = System.IO.Path.GetDirectoryName(dte.Solution.FullName);

			processNamesDictionary.Add(ProcessTypes.ProcessTypeServerJobFirst, "VCSBankServerJob");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeServerJobSecond, "VCSBankServerJob");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeServerJobThird, "VCSBankServerJob");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeServerJobFourth, "VCSBankServerJob");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeServerJobFifth, "VCSBankServerJob");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeServerJobSixth, "VCSBankServerJob");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeServerJobSeventh, "VCSBankServerJob");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeServerJobEigth, "VCSBankServerJob");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeServerJobNinth, "VCSBankServerJob");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeServerJobTenth, "VCSBankServerJob");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeServerJobEleventh, "VCSBankServerJob");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeServerJobTwelfth, "VCSBankServerJob");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeSdelki, "Sdelki");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeAccessAdmin, "CSAccessAdmin");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeCSBAdmin, "CSBAdmin");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeTouchScreen, "CSTouchScreen");
			processNamesDictionary.Add(ProcessTypes.ProcessTypeKasi, "KasiClnt");
		}

		private void InitializeComponent()
		{
			// Initializes the dictionaries
			InitializeProcessTypesDictonary();
			InitializeProcessInfoDictonary();
			InitializeProcessNamesDictonary();

			// Initializes the controls
			InitControls();

			// Fills the list view
			FillListView();

			// Adds the controls to the form
			this.Controls.AddRange(new System.Windows.Forms.Control[] { this.processesView, this.attachButton, this.cancelButton });
		}

		[STAThread]
		public static void Main(string[] args)
		{
			Application.Run(new ProcessesDialog());
		}
	}

	public class ProcessInfo
	{
		public int processID;
		public string processName;
		public int group;
		public string info;

		public ProcessInfo()
		{
		}
	}
}

namespace NotSoSmartAttacher
{
	/// <summary>The object for implementing an Add-in.</summary>
	/// <seealso class='IDTExtensibility2' />
	public class Connect : IDTExtensibility2, IDTCommandTarget
	{
		/// <summary>Implements the constructor for the Add-in object. Place your initialization code within this method.</summary>
		public Connect()
		{
		}

		/// <summary>Implements the OnConnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being loaded.</summary>
		/// <param term='application'>Root object of the host application.</param>
		/// <param term='connectMode'>Describes how the Add-in is being loaded.</param>
		/// <param term='addInInst'>Object representing this Add-in.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnConnection(object application, ext_ConnectMode connectMode, object addInInst, ref Array custom)
		{
			_applicationObject = (DTE2)application;
			_addInInstance = (AddIn)addInInst;
			if(connectMode == ext_ConnectMode.ext_cm_UISetup)
			{
				object []contextGUIDS = new object[] { };
				Commands2 commands = (Commands2)_applicationObject.Commands;
				string toolsMenuName = "Tools";

				//Place the command on the tools menu.
				//Find the MenuBar command bar, which is the top-level command bar holding all the main menu items:
				Microsoft.VisualStudio.CommandBars.CommandBar menuBarCommandBar = ((Microsoft.VisualStudio.CommandBars.CommandBars)_applicationObject.CommandBars)["MenuBar"];

				//Find the Tools command bar on the MenuBar command bar:
				CommandBarControl toolsControl = menuBarCommandBar.Controls[toolsMenuName];
				CommandBarPopup toolsPopup = (CommandBarPopup)toolsControl;

				//This try/catch block can be duplicated if you wish to add multiple commands to be handled by your Add-in,
				//  just make sure you also update the QueryStatus/Exec method to include the new command names.
				try
				{
					//Add a command to the Commands collection:
					Command command = commands.AddNamedCommand2(_addInInstance, "NotSoSmartAttacher", "NotSoSmartAttacher", "Executes the command for NotSoSmartAttacher", true, 59, ref contextGUIDS, (int)vsCommandStatus.vsCommandStatusSupported+(int)vsCommandStatus.vsCommandStatusEnabled, (int)vsCommandStyle.vsCommandStylePictAndText, vsCommandControlType.vsCommandControlTypeButton);

					//Add a control for the command to the tools menu:
					if((command != null) && (toolsPopup != null))
					{
						command.AddControl(toolsPopup.CommandBar, 1);
					}
				}
				catch(System.ArgumentException)
				{
					//If we are here, then the exception is probably because a command with that name
					//  already exists. If so there is no need to recreate the command and we can 
                    //  safely ignore the exception.
				}
			}
		}

		/// <summary>Implements the OnDisconnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being unloaded.</summary>
		/// <param term='disconnectMode'>Describes how the Add-in is being unloaded.</param>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
		{
		}

		/// <summary>Implements the OnAddInsUpdate method of the IDTExtensibility2 interface. Receives notification when the collection of Add-ins has changed.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />		
		public void OnAddInsUpdate(ref Array custom)
		{
		}

		/// <summary>Implements the OnStartupComplete method of the IDTExtensibility2 interface. Receives notification that the host application has completed loading.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnStartupComplete(ref Array custom)
		{
		}

		/// <summary>Implements the OnBeginShutdown method of the IDTExtensibility2 interface. Receives notification that the host application is being unloaded.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnBeginShutdown(ref Array custom)
		{
		}
		
		/// <summary>Implements the QueryStatus method of the IDTCommandTarget interface. This is called when the command's availability is updated</summary>
		/// <param term='commandName'>The name of the command to determine state for.</param>
		/// <param term='neededText'>Text that is needed for the command.</param>
		/// <param term='status'>The state of the command in the user interface.</param>
		/// <param term='commandText'>Text requested by the neededText parameter.</param>
		/// <seealso class='Exec' />
		public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, ref vsCommandStatus status, ref object commandText)
		{
			if(neededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
			{
				if(commandName == "NotSoSmartAttacher.Connect.NotSoSmartAttacher")
				{
					status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported|vsCommandStatus.vsCommandStatusEnabled;
					return;
				}
			}
		}

		/// <summary>Implements the Exec method of the IDTCommandTarget interface. This is called when the command is invoked.</summary>
		/// <param term='commandName'>The name of the command to execute.</param>
		/// <param term='executeOption'>Describes how the command should be run.</param>
		/// <param term='varIn'>Parameters passed from the caller to the command handler.</param>
		/// <param term='varOut'>Parameters passed from the command handler to the caller.</param>
		/// <param term='handled'>Informs the caller if the command was handled or not.</param>
		/// <seealso class='Exec' />
		public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, ref object varOut, ref bool handled)
		{
			handled = false;
			if(executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
			{
				if(commandName == "NotSoSmartAttacher.Connect.NotSoSmartAttacher")
				{
					handled = true;

					ProcessesDialog processesDialog = new ProcessesDialog();
					processesDialog.Text = "NotSoSmartAttacher";
					processesDialog.ControlBox = false;
					processesDialog.MinimizeBox = false;
					processesDialog.MaximizeBox = false;
					processesDialog.FormBorderStyle = FormBorderStyle.FixedSingle;
					processesDialog.Show();

					return;
				}
			}
		}
		private DTE2 _applicationObject;
		private AddIn _addInInstance;
	}
}