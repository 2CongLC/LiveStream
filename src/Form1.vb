Imports System
Imports System.Collections
Imports System.Diagnostics
Imports System.IO
Imports System.IO.Compression
Imports System.Net
Imports System.Net.Sockets
Imports System.Net.WebSockets
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports Microsoft.Win32

Public Class Form1

    Private ls As LiveStream
    Private reg As RegistryKey
#Region "Form1"
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ls = New LiveStream()
        NotifyIcon1.Visible = True
        TextBox2.Text = GetPulicIPAddress()
        TextBox3.Text = GetLocalIPAddress()
        If CheckBox2.Checked = False Then
            reg = Registry.CurrentUser.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Run", True)
            If reg.GetValue("MyStream.exe") <> Nothing Then CheckBox1.Checked = True
        End If
    End Sub
    Private Sub Form1_Resize(sender As Object, e As EventArgs) Handles Me.Resize
        If Me.WindowState = FormWindowState.Minimized AndAlso CheckBox1.Checked = True Then
            Me.ShowInTaskbar = False
            NotifyIcon1.Visible = True
            If ls.IsRunning = True Then
                BalloonTip("Máy chủ đang chạy", GetLocalIPAddress, ToolTipIcon.None)
                NotifyIcon1.Icon = Icon.FromHandle(CType(ImageList1.Images(1), Bitmap).GetHicon)
            Else
                NotifyIcon1.Icon = Icon.FromHandle(CType(ImageList1.Images(0), Bitmap).GetHicon)
            End If
        End If
    End Sub
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        If ls.IsRunning = False Then
            ls.Interval = Integer.Parse(NumericUpDown2.Value)
            ls.Resolution(Integer.Parse(NumericUpDown3.Value), Integer.Parse(NumericUpDown4.Value))
            ls.Start(Integer.Parse(NumericUpDown1.Value))
            Timer1.Start()
            Button1.Text = "Đang chạy ..."
            Button1.Enabled = False
            NumericUpDown1.Enabled = False
            NumericUpDown2.Enabled = False
            NumericUpDown3.Enabled = False
            NumericUpDown4.Enabled = False
        End If
    End Sub

    Private Sub CheckBox2_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox2.CheckedChanged
        If CheckBox2.Checked = True Then
            reg.SetValue("MyStream.exe", Application.ExecutablePath)
        Else
            reg.DeleteValue("MyStream.exe", False)
        End If
    End Sub
#End Region
#Region "Khu vực xử lí NotifyIcon1"
    Private Sub BalloonTip(title As String, text As String, icon As ToolTipIcon)
        NotifyIcon1.BalloonTipTitle = text
        NotifyIcon1.BalloonTipText = title
        NotifyIcon1.BalloonTipIcon = icon
        NotifyIcon1.ShowBalloonTip(5000)
    End Sub
    Private Sub NotifyIcon1_MouseDoubleClick(sender As Object, e As MouseEventArgs) Handles NotifyIcon1.MouseDoubleClick
        UnhideProcess()
    End Sub

    Private Sub NotifyIcon1_MouseClick(sender As Object, e As MouseEventArgs) Handles NotifyIcon1.MouseClick
        If e.Button = MouseButtons.Left Then
            If ls.IsRunning = False Then
                ls.Interval = Integer.Parse(NumericUpDown2.Value)
                ls.Resolution(Integer.Parse(NumericUpDown3.Value), Integer.Parse(NumericUpDown4.Value))
                ls.Start(Integer.Parse(NumericUpDown1.Value))
                Timer1.Start()
                BalloonTip("Máy chủ đang chạy", GetLocalIPAddress, ToolTipIcon.None)
                Button1.Enabled = False
                Button1.Text = "Đang chạy ..."
                NotifyIcon1.Icon = Icon.FromHandle(CType(ImageList1.Images(1), Bitmap).GetHicon)
            End If
        End If
    End Sub

    Private Sub HiênBangQuanLyToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles HiênBangQuanLyToolStripMenuItem.Click
        UnhideProcess()
    End Sub

    Private Sub ThoatToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ThoatToolStripMenuItem.Click
        Me.Close()
    End Sub
#End Region
#Region "Ẩn / Hiện Chương trình xuống khay hệ thống"
    Private Sub HideProcess()
        Me.WindowState = FormWindowState.Minimized
        Me.ShowInTaskbar = False
        NotifyIcon1.Visible = True
    End Sub
    Private Sub UnhideProcess()
        Me.WindowState = FormWindowState.Normal
        Me.ShowInTaskbar = True
        NotifyIcon1.Visible = True
    End Sub
#End Region
#Region "Lấy địa chỉ IP"
    Shared Function GetLocalIPAddress()
        Dim result As String = ""
        Using sock As Socket = New Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.IP)
            sock.Connect("8.8.8.8", 65530)
            Dim ipe As IPEndPoint = CType(sock.LocalEndPoint, IPEndPoint)
            result = ipe.Address.ToString
        End Using
        Return result
    End Function
    Private Function GetPulicIPAddress()
        Try
            If My.Computer.Network.Ping("www.google.com.vn", 1000) = True Then
                Dim result As String = ""
                Dim request As WebRequest = WebRequest.Create("https://ipinfo.io/ip")
                Using response As WebResponse = request.GetResponse()
                    Using stream As StreamReader = New StreamReader(response.GetResponseStream())
                        result = stream.ReadToEnd()
                    End Using
                End Using
                Return result
            End If
        Catch ex As Exception
        End Try
        Return "N/A"
    End Function
#End Region
#Region "Ghi Nhật ký"
    Delegate Sub _xUpdate(ByVal Str As String, ByVal Relay As Boolean)
    Sub Serverlog(ByVal Str As String, ByVal Relay As Boolean)
        On Error Resume Next
        If InvokeRequired Then
            Invoke(New _xUpdate(AddressOf Serverlog), Str, Relay)
        Else
            TextBox1.AppendText(Str & vbNewLine)
        End If
    End Sub
    Sub Log(key As String)
        Dim items As ListViewItem = New ListViewItem(key)
        ListView1.Invoke(CType(Sub()
                                   ListView1.BeginUpdate()
                                   ListView1.Items.Add(items)
                                   ListView1.EndUpdate()
                               End Sub, Action))
    End Sub
#End Region
#Region "Quản lý kết nối"
    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        Dim count As Integer = If(ls.Clients IsNot Nothing, ls.Clients.Count(), 0)
        ToolStripStatusLabel1.Text = "Tổng số kết nối : " & count.ToString()
        If ls.Clients IsNot Nothing Then
            For Each s As Socket In ls.Clients

                Serverlog(s.RemoteEndPoint.ToString, True)

                Exit For
            Next

        End If

    End Sub
#End Region
End Class
