Imports System, System.IO, System.Net, System.Net.Sockets, System.Text, System.Threading

Public Class LiveStream

    Private _Clients As List(Of Socket)
    Private _Thread As Thread
    Public Property ImagesSource As IEnumerable(Of Image)
    Public Property Interval As Integer

    Public ReadOnly Property Clients As IEnumerable(Of Socket)
        Get
            Return _Clients
        End Get
    End Property

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return _Thread IsNot Nothing AndAlso _Thread.IsAlive
        End Get
    End Property
    Public Sub New()
        _Clients = New List(Of Socket)()
        _Thread = Nothing
    End Sub
    Public Sub Resolution(Optional width As Integer = 650, Optional heigth As Integer = 450, Optional showcursor As Boolean = True)
        Me.ImagesSource = DesktopScreen.Snapshots(width, heigth, showcursor)
    End Sub
    Public Sub Start(ByVal port As Integer)
        SyncLock Me
            _Thread = New Thread(New ParameterizedThreadStart(AddressOf ServerThread))
            _Thread.IsBackground = True
            _Thread.Start(port)
        End SyncLock
    End Sub
    Public Sub [Stop]()
        If Me.IsRunning Then
            Try
                _Thread.Join()
                _Thread.Abort()
            Finally
                SyncLock _Clients
                    For Each s In _Clients
                        Try
                            s.Close()
                        Catch
                        End Try
                    Next
                    _Clients.Clear()
                End SyncLock
                _Thread = Nothing
            End Try
        End If
    End Sub
    Private Sub ServerThread(ByVal state As Object)
        Try
            Dim Server As Socket = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            Server.Bind(New IPEndPoint(IPAddress.Any, state))
            Server.Listen(10)
            Debug.WriteLine(String.Format("Server started on port {0}.", state))
            For Each client As Socket In Server.IncommingConnectoins()
                ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf ClientThread), client)
            Next
        Catch
        End Try
        Me.[Stop]()
    End Sub
    Private Sub ClientThread(ByVal client As Object)
        Dim socket As Socket = CType(client, Socket)
        Debug.WriteLine(String.Format("New client from {0}", socket.RemoteEndPoint.ToString()))

        SyncLock _Clients
            _Clients.Add(socket)
        End SyncLock

        Try
            Dim wr As mjpeg = New mjpeg(New NetworkStream(socket, True))
            ' Writes the response header to the client.
            wr.WriteHeader()
            ' Streams the images from the source to the client.
            For Each imgStream In DesktopScreen.Streams(Me.ImagesSource)
                If Me.Interval > 0 Then Thread.Sleep(Me.Interval)
                wr.Write(imgStream)
            Next
            wr.Dispose()
        Catch
        Finally
            SyncLock _Clients
                _Clients.Remove(socket)
            End SyncLock
        End Try
    End Sub
    Public Sub Dispose()
        Me.[Stop]()
    End Sub
End Class
#Region "Module"
Module DesktopScreen
    Public Function Snapshots() As IEnumerable(Of Image)
        Return Snapshots(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, True)
    End Function
    Public Iterator Function Snapshots(width As Integer, height As Integer, showCursor As Boolean) As IEnumerable(Of Image)
        Dim sz As Size = New Size(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height)
        Dim bm As Bitmap = New Bitmap(sz.Width, sz.Height)
        Dim g As Graphics = Graphics.FromImage(bm)
        Dim scaled As Boolean = (width <> sz.Width OrElse height <> sz.Height)
        Dim _bm As Bitmap = bm
        Dim _g As Graphics = g
        If scaled Then
            _bm = New Bitmap(width, height)
            _g = Graphics.FromImage(_bm)
        End If
        Dim src As Rectangle = New Rectangle(0, 0, sz.Width, sz.Height)
        Dim dst As Rectangle = New Rectangle(0, 0, width, height)
        Dim curSize As Size = New Size(32, 32)
        While True
            g.CopyFromScreen(0, 0, 0, 0, sz)
            If showCursor Then Cursors.Default.Draw(g, New Rectangle(Cursor.Position, curSize))
            If scaled Then _g.DrawImage(bm, dst, src, GraphicsUnit.Pixel)
            Yield _bm
        End While
        g.Dispose()
        _g.Dispose()
        bm.Dispose()
        _bm.Dispose()
        Return
    End Function
    Public Iterator Function Streams(ByVal source As IEnumerable(Of Image)) As IEnumerable(Of MemoryStream)
        Dim ms As MemoryStream = New MemoryStream()
        For Each img In source
            ms.SetLength(0)
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg)
            Yield ms
        Next
        ms.Close()
        ms = Nothing
        Return
    End Function
End Module
Module SocketExtensions
    <Runtime.CompilerServices.Extension()>
    Public Iterator Function IncommingConnectoins(ByVal server As Socket) As IEnumerable(Of Socket)
        While True
            Yield server.Accept()
        End While
    End Function
End Module
#End Region
#Region "Images"
Public Class mjpeg
    Private Shared CRLF As Byte() = New Byte() {13, 10}
    Private Shared EmptyLine As Byte() = New Byte() {13, 10, 13, 10}
    Private _Boundary As String, _Stream As Stream
    Public Property Boundary As String
        Get
            Return _Boundary
        End Get
        Private Set(ByVal value As String)
            _Boundary = value
        End Set
    End Property
    Public Property Stream As Stream
        Get
            Return _Stream
        End Get
        Private Set(ByVal value As Stream)
            _Stream = value
        End Set
    End Property
    Public Sub New(ByVal stream As Stream)
        Me.New(stream, "--boundary")
    End Sub
    Public Sub New(s As Stream, str As String)
        Stream = s
        Boundary = str
    End Sub
    Public Sub WriteHeader()
        Write("HTTP/1.1 200 OK" & vbCrLf & "Content-Type: multipart/x-mixed-replace; boundary=" & Me.Boundary & vbCrLf)
        Me.Stream.Flush()
    End Sub
    Public Sub Write(ByVal image As Image)
        Dim ms As MemoryStream = BytesOf(image)
        Me.Write(ms)
    End Sub
    Public Sub Write(ByVal imageStream As MemoryStream)
        Dim sb As StringBuilder = New StringBuilder()
        sb.AppendLine()
        sb.AppendLine(Me.Boundary)
        sb.AppendLine("Content-Type: image/jpeg")
        sb.AppendLine("Content-Length: " & imageStream.Length.ToString())
        sb.AppendLine()
        Write(sb.ToString())
        imageStream.WriteTo(Me.Stream)
        Write(vbCrLf)
        Me.Stream.Flush()
    End Sub
    Private Sub Write(ByVal data As Byte())
        Me.Stream.Write(data, 0, data.Length)
    End Sub
    Private Sub Write(ByVal text As String)
        Dim data As Byte() = BytesOf(text)
        Me.Stream.Write(data, 0, data.Length)
    End Sub
    Private Shared Function BytesOf(ByVal text As String) As Byte()
        Return Encoding.ASCII.GetBytes(text)
    End Function
    Private Shared Function BytesOf(ByVal image As Image) As MemoryStream
        Dim ms As MemoryStream = New MemoryStream()
        image.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg)
        Return ms
    End Function
    Public Function ReadRequest(ByVal length As Integer) As String
        Dim data As Byte() = New Byte(length - 1) {}
        Dim count As Integer = Me.Stream.Read(data, 0, data.Length)
        If count <> 0 Then Return Encoding.ASCII.GetString(data, 0, count)
        Return Nothing
    End Function
    Public Sub Dispose()
        Try
            If Me.Stream IsNot Nothing Then Me.Stream.Dispose()
        Finally
            Me.Stream = Nothing
        End Try
    End Sub
End Class

#End Region