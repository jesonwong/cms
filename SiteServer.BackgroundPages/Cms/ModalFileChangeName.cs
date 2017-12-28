﻿using System;
using System.Collections.Specialized;
using System.Web.UI.WebControls;
using BaiRong.Core;
using SiteServer.CMS.Core;

namespace SiteServer.BackgroundPages.Cms
{
	public class ModalFileChangeName : BasePageCms
    {
        protected Literal LtlFileName;
        protected TextBox TbFileName;

		private string _rootPath;
		private string _directoryPath;

        public static string GetOpenWindowString(int publishmentSystemId, string rootPath, string fileName)
        {
            return LayerUtils.GetOpenScript("修改文件名", PageUtils.GetCmsUrl(nameof(ModalFileChangeName), new NameValueCollection
            {
                {"PublishmentSystemID", publishmentSystemId.ToString()},
                {"RootPath", rootPath},
                {"FileName", fileName}
            }), 400, 250);
        }

        public static string GetOpenWindowString(int publishmentSystemId, string rootPath, string fileName, string hiddenClientId)
        {
            return LayerUtils.GetOpenScript("修改文件名", PageUtils.GetCmsUrl(nameof(ModalFileChangeName), new NameValueCollection
            {
                {"PublishmentSystemID", publishmentSystemId.ToString()},
                {"RootPath", rootPath},
                {"FileName", fileName},
                {"HiddenClientID", hiddenClientId}
            }), 400, 250);
        }

		public void Page_Load(object sender, EventArgs e)
        {
            if (IsForbidden) return;

            PageUtils.CheckRequestParameter("PublishmentSystemID", "RootPath");

            _rootPath = Body.GetQueryString("RootPath").TrimEnd('/');
            _directoryPath = PathUtility.MapPath(PublishmentSystemInfo, _rootPath);

			if (!Page.IsPostBack)
			{
                LtlFileName.Text = Body.GetQueryString("FileName");
			}
		}

        private string RedirectUrl()
        {
            return ModalFileView.GetRedirectUrl(PublishmentSystemId, Body.GetQueryString("rootPath"),
                Body.GetQueryString("FileName"), TbFileName.Text, Body.GetQueryString("HiddenClientID"));
        }

        public override void Submit_OnClick(object sender, EventArgs e)
        {
            if (!DirectoryUtils.IsDirectoryNameCompliant(TbFileName.Text))
            {
                FailMessage("文件名称不符合要求");
                return;
            }

            var path = PathUtils.Combine(_directoryPath, TbFileName.Text);
            if (FileUtils.IsFileExists(path))
            {
                FailMessage("文件已经存在");
                return;
            }
            var pathSource = PathUtils.Combine(_directoryPath, LtlFileName.Text);
            FileUtils.MoveFile(pathSource, path, true);
            FileUtils.DeleteFileIfExists(pathSource);

            Body.AddSiteLog(PublishmentSystemId, "修改文件名", $"文件名:{TbFileName.Text}");
            //JsUtils.SubModal.CloseModalPageWithoutRefresh(Page);
            LayerUtils.CloseAndRedirect(Page, RedirectUrl());
        }
	}
}
