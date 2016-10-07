<%@ Control Language="C#" AutoEventWireup="true" CodeFile="LastWeekDashboardWidget.ascx.cs" Inherits="Plugins.com_bricksandmortarstudio.Reporting.LastWeekDashboardWidget" %>

<asp:UpdatePanel ID="upnlContent" runat="server">
    <ContentTemplate>
        <asp:Panel ID="pnlDashboardTitle" runat="server" CssClass="dashboard-title">
            <asp:Literal runat="server" ID="lDashboardTitle" />
        </asp:Panel>
        <asp:Panel ID="pnlDashboardSubtitle" runat="server" CssClass="dashboard-subtitle">
            <asp:Literal runat="server" ID="lDashboardSubtitle" />
        </asp:Panel>
        <asp:Panel ID="phHtml" runat="server" >
            <asp:Literal runat="server" ID="lHtml"></asp:Literal>
         </asp:Panel>
    </ContentTemplate>
</asp:UpdatePanel>
