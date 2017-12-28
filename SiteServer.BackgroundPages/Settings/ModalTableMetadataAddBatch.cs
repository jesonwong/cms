﻿using System;
using System.Collections;
using System.Collections.Specialized;
using BaiRong.Core;
using BaiRong.Core.Data;
using BaiRong.Core.Model;
using BaiRong.Core.Table;
using SiteServer.Plugin.Models;

namespace SiteServer.BackgroundPages.Settings
{
    public class ModalTableMetadataAddBatch : BasePageCms
    {
        private string _tableName;

        public static string GetOpenWindowStringToAdd(string tableName)
        {
            return PageUtils.GetOpenWindowString("批量添加辅助表字段",
                PageUtils.GetSettingsUrl(nameof(ModalTableMetadataAddBatch), new NameValueCollection
                {
                    {"TableName", tableName}
                }));
        }

        public void Page_Load(object sender, EventArgs e)
        {
            if (IsForbidden) return;

            PageUtils.CheckRequestParameter("TableName");

            _tableName = Body.GetQueryString("TableName");

            if (!IsPostBack)
            {

            }
        }


        public override void Submit_OnClick(object sender, EventArgs e)
        {
            var isChanged = false;

            var attributeNameList = TranslateUtils.StringCollectionToStringList(Request.Form["attributeName"]);
            var dataTypeList = TranslateUtils.StringCollectionToStringList(Request.Form["dataType"]);
            var dataLengthList = TranslateUtils.StringCollectionToStringList(Request.Form["dataLength"]);

            for (var i = 0; i < attributeNameList.Count; i++)
            {
                if (dataTypeList.Count < attributeNameList.Count)
                    dataTypeList.Add(string.Empty);
                if (dataLengthList.Count < attributeNameList.Count)
                    dataLengthList.Add(string.Empty);
            }

            var attributeNameArrayList = TableMetadataManager.GetAttributeNameList(_tableName, true);

            for (var i = 0; i < attributeNameList.Count; i++)
            {
                var attributeName = attributeNameList[i];
                var dataType = dataTypeList[i];
                var dataLength = dataLengthList[i];
                var attributeNameLowercase = attributeName.Trim().ToLower();

                if (attributeNameArrayList.Contains(attributeNameLowercase) || ContentAttribute.AllAttributesLowercase.Contains(attributeNameLowercase))
                {
                    FailMessage("字段添加失败，字段名已存在！");
                }
                else if (!SqlUtils.IsAttributeNameCompliant(attributeName))
                {
                    FailMessage("字段名不符合系统要求！");
                }
                else
                {
                    var info = new TableMetadataInfo
                    {
                        AuxiliaryTableEnName = _tableName,
                        AttributeName = attributeName,
                        DataType = DataTypeUtils.GetEnumType(dataType)
                    };

                    var hashtable = new Hashtable
                    {
                        [DataType.DateTime] = new[] {"8", "false"},
                        [DataType.Integer] = new[] {"4", "false"},
                        [DataType.Text] = new[] {"16", "false"},
                        [DataType.VarChar] = new[] {"255", "true"}
                    };

                    var strArr = (string[])hashtable[DataTypeUtils.GetEnumType(dataType)];
                    if (strArr[1].Equals("false"))
                    {
                        dataLength = strArr[0];
                    }

                    if (string.IsNullOrEmpty(dataLength))
                    {
                        dataLength = strArr[0];
                    }

                    info.DataLength = int.Parse(dataLength);
                    if (info.DataType == DataType.VarChar)
                    {
                        var maxLength = SqlUtils.GetMaxLengthForNVarChar();
                        if (info.DataLength <= 0 || info.DataLength > maxLength)
                        {
                            FailMessage($"字段修改失败，数据长度的值必须位于 1 和 {maxLength} 之间");
                            return;
                        }
                    }
                    info.IsSystem = false;

                    try
                    {
                        BaiRongDataProvider.TableMetadataDao.Insert(info);

                        Body.AddAdminLog("添加辅助表字段",
                            $"辅助表:{_tableName},字段名:{info.AttributeName}");

                        isChanged = true;
                    }
                    catch (Exception ex)
                    {
                        FailMessage(ex, ex.Message);
                    }
                }

            }

            if (isChanged)
            {
                LayerUtils.Close(Page);
            }
        }

    }
}
