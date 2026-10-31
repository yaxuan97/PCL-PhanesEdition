''' <summary>
''' 单个 MCP 会话。HTTP 会话头由传输层维护，协议层只记录协商结果。
''' </summary>
Public Class McpSession

    Public ReadOnly Id As String
    Public ProtocolVersion As String = ""
    Public ClientName As String = ""
    Public ClientVersion As String = ""
    Public CreatedAt As Date = Date.Now
    Public LastSeenAt As Date = Date.Now

    Public Sub New(Id As String)
        Me.Id = If(Id, "")
    End Sub

End Class

''' <summary>
''' 精简的 MCP JSON-RPC 协议实现。
''' </summary>
Public Module McpProtocol

    Public Const LatestProtocolVersion As String = "2025-06-18"
    Public ReadOnly SupportedProtocolVersions As String() = {"2024-11-05", "2025-03-26", "2025-06-18"}

    Private Const InstructionsText As String = "PCL - Phanes Edition MCP 服务器。建议先调用 pcl_get_status 了解启动器状态；涉及启动、停止、修改设置或 Mod 的操作请先确认实例名；删除 Mod、关闭启动器等破坏性操作需要用户在设置中授予完全控制权限。"

    Public Function IsSupportedProtocolVersion(Version As String) As Boolean
        If String.IsNullOrEmpty(Version) Then Return False
        For Each Item As String In SupportedProtocolVersions
            If String.Equals(Item, Version, StringComparison.OrdinalIgnoreCase) Then Return True
        Next
        Return False
    End Function

    Public Function ContainsInitialize(Message As JToken) As Boolean
        If Message Is Nothing Then Return False
        If TypeOf Message Is JObject Then
            Dim MethodToken = Message("method")
            Return MethodToken IsNot Nothing AndAlso MethodToken.ToString() = "initialize"
        ElseIf TypeOf Message Is JArray Then
            For Each Item As JToken In DirectCast(Message, JArray)
                If ContainsInitialize(Item) Then Return True
            Next
        End If
        Return False
    End Function

    ''' <summary>
    ''' 处理单条消息或批量消息。通知返回 Nothing。
    ''' </summary>
    Public Function ProcessMessage(Message As JToken, Session As McpSession) As JToken
        If Message Is Nothing Then Return MakeError(Nothing, -32600, "Invalid Request")
        If TypeOf Message Is JArray Then Return ProcessBatch(DirectCast(Message, JArray), Session)

        Dim Request = TryCast(Message, JObject)
        If Request Is Nothing Then Return MakeError(Nothing, -32600, "Invalid Request")

        Dim MethodToken = Request("method")
        If MethodToken Is Nothing OrElse String.IsNullOrEmpty(MethodToken.ToString()) Then
            If Request("id") IsNot Nothing Then Return MakeError(Request("id"), -32600, "Invalid Request")
            Return Nothing
        End If

        Dim Method As String = MethodToken.ToString()
        Dim Id As JToken = Request("id")

        '没有 id 的消息是通知，不需要响应
        If Id Is Nothing Then Return Nothing

        Select Case Method
            Case "initialize"
                Return HandleInitialize(Request, Id, Session)
            Case "ping"
                Return MakeResult(Id, New JObject())
            Case "tools/list"
                Return MakeResult(Id, McpTools.GetToolListResult())
            Case "tools/call"
                Return HandleToolsCall(Request, Id)
            Case Else
                Return MakeError(Id, -32601, "Method not found: " & Method)
        End Select
    End Function

    Private Function ProcessBatch(Array As JArray, Session As McpSession) As JToken
        Dim Results As New JArray()
        For Each Item As JToken In Array
            Dim Result = ProcessMessage(Item, Session)
            If Result IsNot Nothing Then Results.Add(Result)
        Next
        If Results.Count = 0 Then Return Nothing
        Return Results
    End Function

    Private Function HandleInitialize(Request As JObject, Id As JToken, Session As McpSession) As JObject
        Dim Params = TryCast(Request("params"), JObject)
        If Params Is Nothing Then Params = New JObject()

        Dim RequestedVersion As String = ""
        Dim VersionToken = Params("protocolVersion")
        If VersionToken IsNot Nothing Then RequestedVersion = VersionToken.ToString()

        Dim Negotiated As String = If(IsSupportedProtocolVersion(RequestedVersion), RequestedVersion, LatestProtocolVersion)
        If Session IsNot Nothing Then
            Session.ProtocolVersion = Negotiated
            Dim ClientInfo = TryCast(Params("clientInfo"), JObject)
            If ClientInfo IsNot Nothing Then
                Dim NameToken = ClientInfo("name")
                Dim VersionValueToken = ClientInfo("version")
                If NameToken IsNot Nothing Then Session.ClientName = NameToken.ToString()
                If VersionValueToken IsNot Nothing Then Session.ClientVersion = VersionValueToken.ToString()
            End If
        End If

        Dim Result As New JObject From {
            {"protocolVersion", Negotiated},
            {"capabilities", New JObject From {
                {"tools", New JObject From {{"listChanged", False}}}
            }},
            {"serverInfo", New JObject From {
                {"name", "pcl-phanes"},
                {"version", VersionBaseName}
            }},
            {"instructions", InstructionsText}
        }
        Return MakeResult(Id, Result)
    End Function

    Private Function HandleToolsCall(Request As JObject, Id As JToken) As JObject
        Dim Params = TryCast(Request("params"), JObject)
        If Params Is Nothing Then Return MakeError(Id, -32602, "Missing params")
        Dim NameToken = Params("name")
        If NameToken Is Nothing OrElse String.IsNullOrWhiteSpace(NameToken.ToString()) Then
            Return MakeError(Id, -32602, "Missing tool name")
        End If
        Dim Args = TryCast(Params("arguments"), JObject)
        If Args Is Nothing Then Args = New JObject()
        Return MakeResult(Id, McpTools.Invoke(NameToken.ToString(), Args))
    End Function

    Private Function MakeResult(Id As JToken, Result As JToken) As JObject
        Return New JObject From {
            {"jsonrpc", "2.0"},
            {"id", CloneId(Id)},
            {"result", If(Result Is Nothing, New JObject(), Result)}
        }
    End Function

    Private Function MakeError(Id As JToken, Code As Integer, Message As String) As JObject
        Return New JObject From {
            {"jsonrpc", "2.0"},
            {"id", CloneId(Id)},
            {"error", New JObject From {{"code", Code}, {"message", Message}}}
        }
    End Function

    Private Function CloneId(Id As JToken) As JToken
        If Id Is Nothing Then Return JValue.CreateNull()
        Return Id.DeepClone()
    End Function

End Module
