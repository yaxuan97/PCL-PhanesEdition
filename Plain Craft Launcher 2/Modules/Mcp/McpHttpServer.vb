Imports System.Net
Imports System.Text
Imports System.Threading

''' <summary>
''' 仅监听回环地址的 MCP Streamable HTTP 服务器。
''' </summary>
Public Class McpHttpServer

    Private Const MaxBodyBytes As Integer = 1024 * 1024

    Private ReadOnly _Port As Integer
    Private _Listener As HttpListener = Nothing
    Private _Thread As Thread = Nothing
    Private _Running As Boolean = False
    Private _LastError As String = ""
    Private _StopRequested As Integer = 0
    Private ReadOnly _Sessions As New ConcurrentDictionary(Of String, McpSession)(StringComparer.Ordinal)
    Private ReadOnly _RequestLimit As New SemaphoreSlim(4, 4)
    Private _LastCleanup As Long = 0

    Public Sub New(Port As Integer)
        _Port = Port
    End Sub

    Public ReadOnly Property Port As Integer
        Get
            Return _Port
        End Get
    End Property

    Public ReadOnly Property Endpoint As String
        Get
            Return $"http://127.0.0.1:{_Port}/mcp"
        End Get
    End Property

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return _Running
        End Get
    End Property

    Public ReadOnly Property LastError As String
        Get
            Return _LastError
        End Get
    End Property

    Public ReadOnly Property SessionCount As Integer
        Get
            Return _Sessions.Count
        End Get
    End Property

    ''' <summary>
    ''' 开始监听。失败时返回 False，并设置 LastError。
    ''' </summary>
    Public Function Start() As Boolean
        SyncLock Me
            If _Running Then Return True
            Try
                _StopRequested = 0
                _Listener = New HttpListener()
                _Listener.Prefixes.Add($"http://127.0.0.1:{_Port}/")
                _Listener.Start()
                _Running = True
                _Thread = New Thread(AddressOf ListenLoop) With {
                    .IsBackground = True,
                    .Name = "PCL MCP HTTP Server"
                }
                _Thread.Start()
                Return True
            Catch ex As Exception
                _LastError = DescribeListenerError(ex)
                _Running = False
                If _Listener IsNot Nothing Then
                    Try
                        _Listener.Close()
                    Catch
                    End Try
                    _Listener = Nothing
                End If
                Return False
            End Try
        End SyncLock
    End Function

    ''' <summary>
    ''' 停止监听并清理会话。
    ''' </summary>
    Public Sub [Stop]()
        If Interlocked.Exchange(_StopRequested, 1) <> 0 Then Return
        SyncLock Me
            _Running = False
            If _Listener IsNot Nothing Then
                Try
                    _Listener.Stop()
                Catch
                End Try
                Try
                    _Listener.Close()
                Catch
                End Try
                _Listener = Nothing
            End If
        End SyncLock
        If _Thread IsNot Nothing Then
            Try
                _Thread.Join(1000)
            Catch
            End Try
            _Thread = Nothing
        End If
        _Sessions.Clear()
    End Sub

    Private Function DescribeListenerError(ex As Exception) As String
        Dim ListenerEx = TryCast(ex, HttpListenerException)
        If ListenerEx IsNot Nothing Then
            If ListenerEx.ErrorCode = 5 Then
                Return $"无法监听 127.0.0.1:{_Port}（权限不足，端口可能被系统保留）。"
            ElseIf ListenerEx.ErrorCode = 32 OrElse ListenerEx.ErrorCode = 183 Then
                Return $"端口 {_Port} 已被其他程序占用。"
            End If
        End If
        Return $"无法监听 127.0.0.1:{_Port}：{ex.Message}"
    End Function

    Private Sub ListenLoop()
        While _StopRequested = 0
            Dim Context As HttpListenerContext = Nothing
            Try
                Context = _Listener.GetContext()
            Catch ex As HttpListenerException
                If _StopRequested <> 0 Then Exit While
                _LastError = ex.Message
                Thread.Sleep(50)
                Continue While
            Catch ex As ObjectDisposedException
                Exit While
            Catch ex As Exception
                If _StopRequested <> 0 Then Exit While
                _LastError = ex.Message
                Thread.Sleep(50)
                Continue While
            End Try
            If Context Is Nothing Then Continue While
            Try
                If Not ThreadPool.QueueUserWorkItem(AddressOf HandleContextQueued, Context) Then
                    AbortContext(Context)
                End If
            Catch ex As Exception
                Logger.Warn(ex, "排队 MCP HTTP 请求失败")
                AbortContext(Context)
            End Try
        End While
        _Running = False
    End Sub

    Private Sub HandleContextQueued(State As Object)
        Dim Context = TryCast(State, HttpListenerContext)
        If Context Is Nothing Then Return
        _RequestLimit.Wait()
        Try
            HandleContext(Context)
        Catch ex As Exception
            Logger.Error(ex, "MCP HTTP 请求处理失败", LogBehavior.None)
            Try
                WriteRpcError(Context, 500, -32603, "Internal error")
            Catch
            End Try
        Finally
            _RequestLimit.Release()
        End Try
    End Sub

    Private Sub HandleContext(Context As HttpListenerContext)
        Dim Request As HttpListenerRequest = Context.Request
        Dim Response As HttpListenerResponse = Context.Response
        Try
            SetHeader(Response, "Cache-Control", "no-store")
            Dim RequestPath As String = Request.Url.AbsolutePath

            If Request.HttpMethod = "OPTIONS" Then
                SetHeader(Response, "Allow", "GET, POST, DELETE, OPTIONS")
                WriteEmpty(Context, 204)
                Return
            End If

            If RequestPath = "/health" Then
                If Request.HttpMethod <> "GET" Then
                    SetHeader(Response, "Allow", "GET")
                    WriteRpcError(Context, 405, -32601, "Method not allowed")
                    Return
                End If
                WriteJson(Context, 200, New JObject From {
                    {"status", "ok"},
                    {"name", "PCL - Phanes Edition"},
                    {"version", VersionBaseName},
                    {"pid", Process.GetCurrentProcess().Id},
                    {"mcp", True}
                })
                Return
            End If

            If RequestPath <> "/mcp" Then
                WriteRpcError(Context, 404, -32601, "Not found")
                Return
            End If

            If Not CheckOrigin(Request) Then
                WriteRpcError(Context, 403, -32003, "Origin not allowed")
                Return
            End If

            If Request.HttpMethod = "GET" Then
                SetHeader(Response, "Allow", "POST, DELETE, OPTIONS")
                WriteRpcError(Context, 405, -32601, "SSE is not supported; use POST /mcp")
                Return
            End If

            If Not CheckAuth(Request) Then
                SetHeader(Response, "WWW-Authenticate", "Bearer")
                WriteRpcError(Context, 401, -32001, "Unauthorized")
                Return
            End If

            If Request.HttpMethod = "DELETE" Then
                Dim DeleteSessionId As String = Request.Headers("Mcp-Session-Id")
                If Not String.IsNullOrEmpty(DeleteSessionId) Then
                    Dim Removed As McpSession = Nothing
                    _Sessions.TryRemove(DeleteSessionId, Removed)
                End If
                WriteEmpty(Context, 204)
                Return
            End If

            If Request.HttpMethod <> "POST" Then
                SetHeader(Response, "Allow", "POST, DELETE, OPTIONS")
                WriteRpcError(Context, 405, -32601, "Method not allowed")
                Return
            End If

            If Not CheckContentType(Request) Then
                WriteRpcError(Context, 415, -32600, "Content-Type must be application/json")
                Return
            End If

            Dim TooLarge As Boolean = False
            Dim Body As String = ReadBody(Request, TooLarge)
            If TooLarge Then
                WriteRpcError(Context, 413, -32600, "Request body too large")
                Return
            End If
            If String.IsNullOrWhiteSpace(Body) Then
                WriteRpcError(Context, 400, -32700, "Parse error")
                Return
            End If

            Dim Message As JToken = Nothing
            Try
                Message = JToken.Parse(Body)
            Catch ex As Exception
                WriteRpcError(Context, 400, -32700, "Parse error")
                Return
            End Try

            Dim ProtocolHeader As String = Request.Headers("MCP-Protocol-Version")
            If Not String.IsNullOrEmpty(ProtocolHeader) AndAlso Not McpProtocol.IsSupportedProtocolVersion(ProtocolHeader) Then
                WriteRpcError(Context, 400, -32600, "Unsupported MCP-Protocol-Version: " & ProtocolHeader)
                Return
            End If

            CleanupSessions()
            Dim RequestedSessionId As String = Request.Headers("Mcp-Session-Id")
            Dim HasInitialize As Boolean = McpProtocol.ContainsInitialize(Message)
            Dim Session As McpSession = Nothing

            If Not String.IsNullOrEmpty(RequestedSessionId) Then
                If Not _Sessions.TryGetValue(RequestedSessionId, Session) Then
                    If Not HasInitialize Then
                        WriteJson(Context, 404, MakeSessionError(), Nothing)
                        Return
                    End If
                    Session = New McpSession(RequestedSessionId)
                End If
            ElseIf HasInitialize Then
                Session = New McpSession(Guid.NewGuid().ToString("N"))
            Else
                '不带会话时按无状态请求处理，兼容只做 tools/call 的简单客户端
                Session = New McpSession("")
            End If
            Session.LastSeenAt = Date.Now

            Dim Result As JToken = McpProtocol.ProcessMessage(Message, Session)
            If Not String.IsNullOrEmpty(Session.Id) Then
                _Sessions(Session.Id) = Session
            End If

            If Result Is Nothing Then
                WriteEmpty(Context, 202)
            Else
                WriteJson(Context, 200, Result, Session)
            End If
        Catch ex As Exception
            Logger.Error(ex, "处理 MCP HTTP 请求失败", LogBehavior.None)
            Try
                WriteRpcError(Context, 500, -32603, "Internal error")
            Catch
            End Try
        Finally
            Try
                Response.Close()
            Catch
            End Try
        End Try
    End Sub

    Private Function CheckAuth(Request As HttpListenerRequest) As Boolean
        Dim Header As String = Request.Headers("Authorization")
        If String.IsNullOrEmpty(Header) OrElse Not Header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) Then Return False
        Dim Supplied As String = Header.Substring(7).Trim()
        Dim Expected As String = ModMcp.GetToken()
        If String.IsNullOrEmpty(Expected) Then Return False
        Return SecureEquals(Supplied, Expected)
    End Function

    Private Function CheckOrigin(Request As HttpListenerRequest) As Boolean
        Dim Origin As String = Request.Headers("Origin")
        If String.IsNullOrEmpty(Origin) OrElse Origin = "null" Then Return True
        Dim Parsed As Uri = Nothing
        If Not Uri.TryCreate(Origin, UriKind.Absolute, Parsed) Then Return False
        If Parsed.Scheme = "vscode-webview" OrElse Parsed.Scheme = "vscode-file" Then Return True
        If Parsed.Host = "127.0.0.1" OrElse Parsed.Host = "::1" OrElse
           Parsed.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) Then Return True
        Return False
    End Function

    Private Function CheckContentType(Request As HttpListenerRequest) As Boolean
        Dim ContentType As String = Request.ContentType
        If String.IsNullOrEmpty(ContentType) Then Return False
        Return ContentType.Split(";"c)(0).Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function ReadBody(Request As HttpListenerRequest, ByRef TooLarge As Boolean) As String
        If Request.ContentLength64 > MaxBodyBytes Then
            TooLarge = True
            Return Nothing
        End If
        Using Stream As New MemoryStream()
            Dim Buffer(8191) As Byte
            Dim Total As Integer = 0
            Do
                Dim ReadCount As Integer = Request.InputStream.Read(Buffer, 0, Buffer.Length)
                If ReadCount <= 0 Then Exit Do
                Total += ReadCount
                If Total > MaxBodyBytes Then
                    TooLarge = True
                    Return Nothing
                End If
                Stream.Write(Buffer, 0, ReadCount)
            Loop
            Return Encoding.UTF8.GetString(Stream.ToArray())
        End Using
    End Function

    Private Sub CleanupSessions()
        Dim Now As Long = GetTimeMs()
        If Now - _LastCleanup < 60000 Then Return
        _LastCleanup = Now
        Dim Current As Date = Date.Now
        For Each Pair As KeyValuePair(Of String, McpSession) In _Sessions.ToArray()
            If (Current - Pair.Value.LastSeenAt).TotalHours >= 1 Then
                Dim Removed As McpSession = Nothing
                _Sessions.TryRemove(Pair.Key, Removed)
            End If
        Next
    End Sub

    Private Function MakeSessionError() As JObject
        Return New JObject From {
            {"jsonrpc", "2.0"},
            {"id", JValue.CreateNull()},
            {"error", New JObject From {{"code", -32000}, {"message", "Session not found"}}}
        }
    End Function

    Private Shared Function SecureEquals(A As String, B As String) As Boolean
        If A Is Nothing OrElse B Is Nothing Then Return False
        Dim Difference As Integer = A.Length Xor B.Length
        Dim Count As Integer = Math.Min(A.Length, B.Length)
        For i = 0 To Count - 1
            Difference = Difference Or (AscW(A(i)) Xor AscW(B(i)))
        Next
        Return Difference = 0
    End Function

    Private Sub SetHeader(Response As HttpListenerResponse, Name As String, Value As String)
        Try
            Response.Headers(Name) = Value
        Catch
        End Try
    End Sub

    Private Sub WriteJson(Context As HttpListenerContext, Status As Integer, Body As JToken, Optional Session As McpSession = Nothing)
        Dim Response As HttpListenerResponse = Context.Response
        Response.StatusCode = Status
        Response.ContentType = "application/json; charset=utf-8"
        Response.ContentEncoding = Encoding.UTF8
        SetHeader(Response, "Cache-Control", "no-store")
        If Session IsNot Nothing Then
            If Not String.IsNullOrEmpty(Session.Id) Then SetHeader(Response, "Mcp-Session-Id", Session.Id)
            Dim Version As String = If(String.IsNullOrEmpty(Session.ProtocolVersion), McpProtocol.LatestProtocolVersion, Session.ProtocolVersion)
            SetHeader(Response, "MCP-Protocol-Version", Version)
        Else
            SetHeader(Response, "MCP-Protocol-Version", McpProtocol.LatestProtocolVersion)
        End If
        Dim Bytes As Byte() = Encoding.UTF8.GetBytes(Body.ToString(Newtonsoft.Json.Formatting.None))
        Response.ContentLength64 = Bytes.Length
        Response.OutputStream.Write(Bytes, 0, Bytes.Length)
    End Sub

    Private Sub WriteEmpty(Context As HttpListenerContext, Status As Integer)
        Dim Response As HttpListenerResponse = Context.Response
        Response.StatusCode = Status
        Response.ContentLength64 = 0
    End Sub

    Private Sub WriteRpcError(Context As HttpListenerContext, Status As Integer, Code As Integer, Message As String)
        Dim Body As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", JValue.CreateNull()},
            {"error", New JObject From {{"code", Code}, {"message", Message}}}
        }
        WriteJson(Context, Status, Body)
    End Sub

    Private Sub AbortContext(Context As HttpListenerContext)
        Try
            Context.Response.Abort()
        Catch
        End Try
    End Sub

End Class
