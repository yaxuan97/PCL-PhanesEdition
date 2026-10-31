Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text

''' <summary>
''' 将 stdio 的 MCP 消息转发到正在运行的启动器 HTTP 服务器。
''' </summary>
Public Module McpStdioBridge

    Public Sub Run()
        Dim ConfigPath As String = Paths.Base & "PCL-Phanes\McpClient.json"
        Dim Url As String = ""
        Dim Token As String = ""
        If Not LoadConfig(ConfigPath, Url, Token) Then
            Console.Error.WriteLine("PCL MCP 桥接：未找到有效的客户端配置 " & ConfigPath)
            Console.Error.WriteLine("请先启动 PCL - Phanes Edition，并在 设置 → 其他 → AI 接入（MCP） 中启用 MCP 服务器。")
            Environment.Exit(2)
            Return
        End If

        Dim Input As New StreamReader(Console.OpenStandardInput(), New UTF8Encoding(False), False)
        Dim Output As New StreamWriter(Console.OpenStandardOutput(), New UTF8Encoding(False)) With {.AutoFlush = True}
        Dim Client As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(120)}
        Dim SessionId As String = ""

        Do
            Dim Line As String = Nothing
            Try
                Line = Input.ReadLine()
            Catch ex As Exception
                Console.Error.WriteLine("PCL MCP 桥接：读取 stdin 失败：" & ex.Message)
                Exit Do
            End Try
            If Line Is Nothing Then Exit Do
            Line = Line.Trim()
            If Line = "" Then Continue Do

            Dim HasId As Boolean = RequestHasId(Line)
            Try
                Dim ResponseText As String = Send(Client, Url, Token, SessionId, Line, ConfigPath)
                If Not String.IsNullOrEmpty(ResponseText) Then Output.WriteLine(ResponseText)
            Catch ex As Exception
                Console.Error.WriteLine("PCL MCP 桥接：转发失败：" & ex.Message)
                If HasId Then Output.WriteLine(MakeErrorFromBody(Line, -32603, ex.Message))
            End Try
        Loop

        Client.Dispose()
    End Sub

    Private Function LoadConfig(ConfigPath As String, ByRef Url As String, ByRef Token As String) As Boolean
        Try
            If Not File.Exists(ConfigPath) Then Return False
            Dim Config As JObject = JObject.Parse(File.ReadAllText(ConfigPath, Encoding.UTF8))
            Dim UrlToken = Config("url")
            Dim TokenToken = Config("token")
            Url = If(UrlToken Is Nothing, "", UrlToken.ToString())
            Token = If(TokenToken Is Nothing, "", TokenToken.ToString())
            Return Not String.IsNullOrEmpty(Url) AndAlso Not String.IsNullOrEmpty(Token)
        Catch ex As Exception
            Console.Error.WriteLine("PCL MCP 桥接：读取配置失败：" & ex.Message)
            Return False
        End Try
    End Function

    Private Function Send(Client As HttpClient, ByRef Url As String, ByRef Token As String, ByRef SessionId As String,
                          Body As String, ConfigPath As String) As String
        For Attempt As Integer = 0 To 1
            Using Request As New HttpRequestMessage(HttpMethod.Post, Url)
                Request.Content = New StringContent(Body, Encoding.UTF8, "application/json")
                Request.Headers.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
                Request.Headers.Accept.Add(New MediaTypeWithQualityHeaderValue("text/event-stream"))
                Request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", Token)
                If Not String.IsNullOrEmpty(SessionId) Then Request.Headers.Add("Mcp-Session-Id", SessionId)

                Using Response As HttpResponseMessage = Client.SendAsync(Request).GetAwaiter().GetResult()
                    Dim ResponseText As String = Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    UpdateSession(Response, SessionId)

                    If Response.StatusCode = HttpStatusCode.Unauthorized AndAlso Attempt = 0 Then
                        Dim NewUrl As String = ""
                        Dim NewToken As String = ""
                        If LoadConfig(ConfigPath, NewUrl, NewToken) Then
                            Url = NewUrl
                            Token = NewToken
                            Continue For
                        End If
                    End If

                    If Response.StatusCode = HttpStatusCode.NotFound AndAlso Attempt = 0 AndAlso Not String.IsNullOrEmpty(SessionId) Then
                        SessionId = ""
                        Continue For
                    End If

                    If Response.IsSuccessStatusCode Then Return ResponseText
                    If String.IsNullOrEmpty(ResponseText) Then ResponseText = "HTTP " & CInt(Response.StatusCode)
                    Return MakeErrorFromBody(Body, CInt(Response.StatusCode), ResponseText)
                End Using
            End Using
        Next
        Return ""
    End Function

    Private Sub UpdateSession(Response As HttpResponseMessage, ByRef SessionId As String)
        Dim Values As IEnumerable(Of String) = Nothing
        If Response.Headers.TryGetValues("Mcp-Session-Id", Values) Then
            Dim NewSession As String = Values.FirstOrDefault()
            If Not String.IsNullOrEmpty(NewSession) Then SessionId = NewSession
        End If
    End Sub

    Private Function RequestHasId(Body As String) As Boolean
        Try
            Return TokenHasId(JToken.Parse(Body))
        Catch
            Return True
        End Try
    End Function

    Private Function TokenHasId(Token As JToken) As Boolean
        If Token Is Nothing Then Return False
        If TypeOf Token Is JObject Then Return Token("id") IsNot Nothing
        If TypeOf Token Is JArray Then
            For Each Item As JToken In DirectCast(Token, JArray)
                If TokenHasId(Item) Then Return True
            Next
        End If
        Return False
    End Function

    Private Function MakeErrorFromBody(Body As String, Code As Integer, Message As String) As String
        Dim Id As JToken = JValue.CreateNull()
        Try
            Id = FindFirstId(JToken.Parse(Body))
        Catch
        End Try
        Dim ErrorObject As New JObject From {
            {"jsonrpc", "2.0"},
            {"id", Id},
            {"error", New JObject From {{"code", Code}, {"message", Message}}}
        }
        Return ErrorObject.ToString(Newtonsoft.Json.Formatting.None)
    End Function

    Private Function FindFirstId(Token As JToken) As JToken
        If Token Is Nothing Then Return JValue.CreateNull()
        If TypeOf Token Is JObject Then
            Dim Id = Token("id")
            If Id IsNot Nothing Then Return Id.DeepClone()
        ElseIf TypeOf Token Is JArray Then
            For Each Item As JToken In DirectCast(Token, JArray)
                Dim Id = FindFirstId(Item)
                If Id IsNot Nothing AndAlso Id.Type <> JTokenType.Null Then Return Id
            Next
        End If
        Return JValue.CreateNull()
    End Function

End Module
