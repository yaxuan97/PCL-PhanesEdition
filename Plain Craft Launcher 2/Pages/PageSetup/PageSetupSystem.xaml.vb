Public Class PageSetupSystem


    Private Sub PageSetupSystem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        Static IsLoaded As Boolean = False
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        SliderLoad()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload()
        SettingService.RefreshSettings(Me)
        RefreshMcpStatus()
    End Sub
    Public Sub Reset()
        Try
            SettingService.ResetSettings(Me)
            Logger.Info("已初始化启动器页设置")
            Hint("已初始化启动器页设置！", HintType.Green, False)
        Catch ex As Exception
            Logger.Error(ex, "初始化启动器页设置失败", LogBehavior.Alert)
        End Try
        Reload()
    End Sub

    '滑动条
    Private Sub SliderLoad()
        SliderDownloadThread.GetHintText = Function(v) v + 1
        SliderDownloadSpeed.GetHintText =
        Function(v)
            Select Case v
                Case Is <= 14
                    Return (v + 1) * 0.1 & " M/s"
                Case Is <= 31
                    Return (v - 11) * 0.5 & " M/s"
                Case Is <= 41
                    Return (v - 21) & " M/s"
                Case Else
                    Return "无限制"
            End Select
        End Function
        SliderDebugAnim.GetHintText = Function(v) If(v > 29, "关闭", (v / 10 + 0.1) & "x")
    End Sub
    Private Sub SliderDownloadThread_PreviewChange(sender As Object, e As RouteEventArgs) Handles SliderDownloadThread.PreviewChange
        If SliderDownloadThread.Value < 100 Then Return
        If Not Settings.Get(Of Boolean)("HintDownloadThread") Then
            Settings.Set("HintDownloadThread", True)
            MyMsgBox("如果设置过多的下载线程，可能会导致下载时出现非常严重的卡顿。" & vbCrLf &
                     "一般设置 64 线程即可满足大多数下载需求，除非你知道你在干什么，否则不建议设置更多的线程数！", "警告", "我知道了", IsWarn:=True)
        End If
    End Sub

    '调试模式
    Private Sub CheckDebugMode_Change() Handles CheckDebugMode.Change
        If AniControlEnabled = 0 Then Hint("部分调试信息将在刷新或启动器重启后切换显示！",, False)
    End Sub

#Region "导出 / 导入设置"

    Private Sub BtnSystemSettingExp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingExp.Click
        Hint("该功能尚在开发中！")
    End Sub
    Private Sub BtnSystemSettingImp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSystemSettingImp.Click
        Hint("该功能尚在开发中！")
    End Sub

#End Region

#Region "MCP"

    Private Sub RefreshMcpStatus()
        Try
            Dim Token As String = ModMcp.GetToken()
            TextMcpToken.Text = Token
            BtnMcpCopyToken.IsEnabled = Not String.IsNullOrEmpty(Token)
            BtnMcpRegenToken.IsEnabled = Settings.Get(Of Boolean)("SystemMcpServerEnabled")
            If ModMcp.IsRunning Then
                LabMcpStatus.Text = $"运行中：{ModMcp.Endpoint}（权限：{McpPermissionName(ModMcp.Permission)}）"
            ElseIf Settings.Get(Of Boolean)("SystemMcpServerEnabled") Then
                LabMcpStatus.Text = "启动失败：" & If(String.IsNullOrEmpty(ModMcp.LastError), "未知错误", ModMcp.LastError)
            Else
                LabMcpStatus.Text = "未启用。启用后 AI 客户端可通过本机 MCP 接入；访问令牌会在启用时自动生成。"
            End If
        Catch ex As Exception
            LabMcpStatus.Text = "状态刷新失败：" & ex.Message
        End Try
    End Sub

    Private Function McpPermissionName(Value As Integer) As String
        Select Case Value
            Case 0
                Return "只读"
            Case 1
                Return "标准控制"
            Case Else
                Return "完全控制"
        End Select
    End Function

    Private Sub ScheduleMcpStatusRefresh()
        RunInNewThread(
        Sub()
            Thread.Sleep(150)
            RunInUi(AddressOf RefreshMcpStatus)
        End Sub, "MCP Status Refresh")
    End Sub

    Private Sub CheckMcpEnable_Change(sender As Object, user As Boolean) Handles CheckMcpEnable.Change
        ScheduleMcpStatusRefresh()
    End Sub

    Private Sub ComboMcpPermission_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboMcpPermission.SelectionChanged
        ScheduleMcpStatusRefresh()
    End Sub

    Private Sub TextMcpPort_ValidatedTextChanged(sender As Object, e As RoutedEventArgs) Handles TextMcpPort.ValidatedTextChanged
        ScheduleMcpStatusRefresh()
    End Sub

    Private Sub BtnMcpCopyToken_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnMcpCopyToken.Click
        Dim Token As String = ModMcp.GetToken()
        If String.IsNullOrEmpty(Token) Then
            Hint("请先启用 MCP 服务器以生成访问令牌。", HintType.Red)
            Return
        End If
        ClipboardSet(Token)
    End Sub

    Private Sub BtnMcpRegenToken_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnMcpRegenToken.Click
        If MyMsgBox("重新生成后，所有已经配置的 AI 客户端都需要更新令牌才能继续连接。确定继续吗？",
                    "重新生成 MCP 令牌", "重新生成", "取消", IsWarn:=True) <> 1 Then Return
        ModMcp.RegenerateToken()
        RefreshMcpStatus()
        Hint("已重新生成 MCP 访问令牌。", HintType.Green, False)
    End Sub

    Private Sub BtnMcpCopyConfig_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnMcpCopyConfig.Click
        Dim ClientArgs As New JArray()
        ClientArgs.Add("--mcp")
        Dim Config As New JObject From {
            {"mcpServers", New JObject From {
                {"pcl-phanes", New JObject From {
                    {"command", PathExe},
                    {"args", ClientArgs}
                }}
            }}
        }
        ClipboardSet(Config.ToString(Newtonsoft.Json.Formatting.Indented), False)
        Hint("已复制 MCP 客户端配置（stdio 桥接）。", HintType.Green, False)
    End Sub

#End Region

End Class
