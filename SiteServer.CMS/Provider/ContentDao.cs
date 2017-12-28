﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Text;
using BaiRong.Core;
using BaiRong.Core.Data;
using BaiRong.Core.Model;
using BaiRong.Core.Model.Attributes;
using BaiRong.Core.Model.Enumerations;
using BaiRong.Core.Table;
using SiteServer.CMS.Core;
using SiteServer.CMS.Model;
using SiteServer.CMS.StlParser.Cache;
using SiteServer.Plugin.Models;

namespace SiteServer.CMS.Provider
{
    public class ContentDao : DataProviderBase
    {
        public int GetTaxisToInsert(string tableName, int nodeId, bool isTop)
        {
            int taxis;

            if (isTop)
            {
                taxis = BaiRongDataProvider.ContentDao.GetMaxTaxis(tableName, nodeId, true) + 1;
            }
            else
            {
                taxis = BaiRongDataProvider.ContentDao.GetMaxTaxis(tableName, nodeId, false) + 1;
            }

            return taxis;
        }

        public int Insert(string tableName, PublishmentSystemInfo publishmentSystemInfo, IContentInfo contentInfo)
        {
            var taxis = GetTaxisToInsert(tableName, contentInfo.NodeId, contentInfo.IsTop);
            return Insert(tableName, publishmentSystemInfo, contentInfo, true, taxis);
        }

        public int InsertPreview(string tableName, PublishmentSystemInfo publishmentSystemInfo, NodeInfo nodeInfo, ContentInfo contentInfo)
        {
            nodeInfo.Additional.IsPreviewContents = true;
            DataProvider.NodeDao.UpdateAdditional(nodeInfo);

            contentInfo.SourceId = SourceManager.Preview;
            return Insert(tableName, publishmentSystemInfo, contentInfo, false, 0);
        }

        public int Insert(string tableName, PublishmentSystemInfo publishmentSystemInfo, IContentInfo contentInfo, bool isUpdateContentNum, int taxis)
        {
            var contentId = 0;

            if (!string.IsNullOrEmpty(tableName))
            {
                if (publishmentSystemInfo.Additional.IsAutoPageInTextEditor && contentInfo.ContainsKey(BackgroundContentAttribute.Content))
                {
                    contentInfo.Set(BackgroundContentAttribute.Content, ContentUtility.GetAutoPageContent(contentInfo.GetString(BackgroundContentAttribute.Content), publishmentSystemInfo.Additional.AutoPageWordNum));
                }

                contentInfo.Taxis = taxis;

                contentId = BaiRongDataProvider.ContentDao.Insert(tableName, contentInfo);

                if (isUpdateContentNum)
                {
                    new Action(() =>
                    {
                        DataProvider.NodeDao.UpdateContentNum(PublishmentSystemManager.GetPublishmentSystemInfo(contentInfo.PublishmentSystemId), contentInfo.NodeId, true);
                    }).BeginInvoke(null, null);
                }

                ClearStlCache();
            }

            return contentId;
        }

        private static void ClearStlCache()
        {
            new Action(Content.ClearCache).BeginInvoke(null, null);
        }

        public void Update(string tableName, PublishmentSystemInfo publishmentSystemInfo, IContentInfo contentInfo)
        {
            if (publishmentSystemInfo.Additional.IsAutoPageInTextEditor && contentInfo.ContainsKey(BackgroundContentAttribute.Content))
            {
                contentInfo.Set(BackgroundContentAttribute.Content, ContentUtility.GetAutoPageContent(contentInfo.GetString(BackgroundContentAttribute.Content), publishmentSystemInfo.Additional.AutoPageWordNum));
            }

            BaiRongDataProvider.ContentDao.Update(tableName, contentInfo);

            ClearStlCache();
        }

        public void UpdateAutoPageContent(string tableName, PublishmentSystemInfo publishmentSystemInfo)
        {
            if (publishmentSystemInfo.Additional.IsAutoPageInTextEditor)
            {
                string sqlString =
                    $"SELECT Id, {BackgroundContentAttribute.Content} FROM {tableName} WHERE (PublishmentSystemId = {publishmentSystemInfo.PublishmentSystemId})";

                using (var rdr = ExecuteReader(sqlString))
                {
                    while (rdr.Read())
                    {
                        var contentId = GetInt(rdr, 0);
                        var content = GetString(rdr, 1);
                        if (!string.IsNullOrEmpty(content))
                        {
                            content = ContentUtility.GetAutoPageContent(content, publishmentSystemInfo.Additional.AutoPageWordNum);
                            string updateString =
                                $"UPDATE {tableName} SET {BackgroundContentAttribute.Content} = '{content}' WHERE Id = {contentId}";
                            try
                            {
                                ExecuteNonQuery(updateString);
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }

                    rdr.Close();
                }
            }
        }

        public ContentInfo GetContentInfoNotTrash(string tableName, int contentId)
        {
            ContentInfo info = null;
            if (contentId > 0)
            {
                if (!string.IsNullOrEmpty(tableName))
                {
                    string sqlWhere = $"WHERE NodeId > 0 AND Id = {contentId}";
                    var sqlSelect = BaiRongDataProvider.DatabaseDao.GetSelectSqlString(tableName, SqlUtils.Asterisk, sqlWhere);

                    using (var rdr = ExecuteReader(sqlSelect))
                    {
                        if (rdr.Read())
                        {
                            info = GetContentInfo(rdr);
                        }
                        rdr.Close();
                    }
                }
            }

            return info;
        }

        private ContentInfo GetContentInfo(IDataReader rdr)
        {
            var contentInfo = new ContentInfo();
            contentInfo.Load(rdr);
            contentInfo.Load(contentInfo.SettingsXml);

            return contentInfo;
        }

        public ContentInfo GetContentInfo(string tableName, int contentId)
        {
            if (string.IsNullOrEmpty(tableName) || contentId <= 0) return null;

            ContentInfo info = null;

            string sqlWhere = $"WHERE Id = {contentId}";
            var sqlSelect = BaiRongDataProvider.DatabaseDao.GetSelectSqlString(tableName, SqlUtils.Asterisk, sqlWhere);

            using (var rdr = ExecuteReader(sqlSelect))
            {
                if (rdr.Read())
                {
                    info = GetContentInfo(rdr);
                }
                rdr.Close();
            }

            return info;
        }

        public int GetCountOfContentAdd(string tableName, int publishmentSystemId, int nodeId, EScopeType scope, DateTime begin, DateTime end, string userName)
        {
            var nodeIdList = DataProvider.NodeDao.GetNodeIdListByScopeType(nodeId, scope, string.Empty, string.Empty);
            return BaiRongDataProvider.ContentDao.GetCountOfContentAdd(tableName, publishmentSystemId, nodeIdList, begin, end, userName);
        }

        public int GetCountOfContentUpdate(string tableName, int publishmentSystemId, int nodeId, EScopeType scope, DateTime begin, DateTime end, string userName)
        {
            var nodeIdList = DataProvider.NodeDao.GetNodeIdListByScopeType(nodeId, scope, string.Empty, string.Empty);
            return BaiRongDataProvider.ContentDao.GetCountOfContentUpdate(tableName, publishmentSystemId, nodeIdList, begin, end, userName);
        }

        public List<int> GetContentIdListChecked(string tableName, int nodeId, int totalNum, string orderByFormatString, string whereString)
        {
            var nodeIdList = new List<int>
            {
                nodeId
            };
            return BaiRongDataProvider.ContentDao.GetContentIdListChecked(tableName, nodeIdList, totalNum, orderByFormatString, whereString);
        }

        public List<int> GetContentIdListChecked(string tableName, int nodeId, string orderByFormatString, string whereString)
        {
            return GetContentIdListChecked(tableName, nodeId, 0, orderByFormatString, whereString);
        }

        public List<int> GetContentIdListChecked(string tableName, int nodeId, int totalNum, string orderByFormatString)
        {
            return GetContentIdListChecked(tableName, nodeId, totalNum, orderByFormatString, string.Empty);
        }

        public List<int> GetContentIdListChecked(string tableName, int nodeId, string orderByFormatString)
        {
            return GetContentIdListChecked(tableName, nodeId, orderByFormatString, string.Empty);
        }

        public void TrashContents(int publishmentSystemId, string tableName, List<int> contentIdList, int nodeId)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                var referenceIdList = BaiRongDataProvider.ContentDao.GetReferenceIdList(tableName, contentIdList);
                if (referenceIdList.Count > 0)
                {
                    DeleteContents(publishmentSystemId, tableName, referenceIdList);
                }
                var updateNum = BaiRongDataProvider.ContentDao.TrashContents(publishmentSystemId, tableName, contentIdList);
                if (updateNum > 0)
                {
                    new Action(() =>
                    {
                        DataProvider.NodeDao.UpdateContentNum(PublishmentSystemManager.GetPublishmentSystemInfo(publishmentSystemId), nodeId, true);
                    }).BeginInvoke(null, null);
                }
            }
        }

        public void TrashContents(int publishmentSystemId, string tableName, List<int> contentIdArrayList)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                var referenceIdList = BaiRongDataProvider.ContentDao.GetReferenceIdList(tableName, contentIdArrayList);
                if (referenceIdList.Count > 0)
                {
                    DeleteContents(publishmentSystemId, tableName, referenceIdList);
                }
                var updateNum = BaiRongDataProvider.ContentDao.TrashContents(publishmentSystemId, tableName, contentIdArrayList);
                if (updateNum > 0)
                {
                    new Action(() =>
                    {
                        DataProvider.NodeDao.UpdateContentNum(PublishmentSystemManager.GetPublishmentSystemInfo(publishmentSystemId));
                    }).BeginInvoke(null, null);
                }
            }
        }

        public void TrashContentsByNodeId(int publishmentSystemId, string tableName, int nodeId)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                var contentIdList = BaiRongDataProvider.ContentDao.GetContentIdList(tableName, nodeId);
                var referenceIdList = BaiRongDataProvider.ContentDao.GetReferenceIdList(tableName, contentIdList);
                if (referenceIdList.Count > 0)
                {
                    DeleteContents(publishmentSystemId, tableName, referenceIdList);
                }
                var updateNum = BaiRongDataProvider.ContentDao.TrashContentsByNodeId(publishmentSystemId, tableName, nodeId);
                if (updateNum > 0)
                {
                    new Action(() =>
                    {
                        DataProvider.NodeDao.UpdateContentNum(PublishmentSystemManager.GetPublishmentSystemInfo(publishmentSystemId), nodeId, true);
                    }).BeginInvoke(null, null);
                }
            }
        }

        public void DeleteContents(int publishmentSystemId, string tableName, List<int> contentIdList, int nodeId)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                var deleteNum = BaiRongDataProvider.ContentDao.DeleteContents(publishmentSystemId, tableName, contentIdList);

                if (nodeId > 0 && deleteNum > 0)
                {
                    new Action(() =>
                    {
                        DataProvider.NodeDao.UpdateContentNum(PublishmentSystemManager.GetPublishmentSystemInfo(publishmentSystemId), nodeId, true);
                    }).BeginInvoke(null, null);
                }

                ClearStlCache();
            }
        }

        private void DeleteContents(int publishmentSystemId, string tableName, List<int> contentIdList)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                var deleteNum = BaiRongDataProvider.ContentDao.DeleteContents(publishmentSystemId, tableName, contentIdList);
                if (deleteNum > 0)
                {
                    new Action(() =>
                    {
                        DataProvider.NodeDao.UpdateContentNum(PublishmentSystemManager.GetPublishmentSystemInfo(publishmentSystemId));
                    }).BeginInvoke(null, null);
                }

                ClearStlCache();
            }
        }

        public void DeleteContentsByNodeId(int publishmentSystemId, string tableName, int nodeId)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                var contentIdList = GetContentIdListChecked(tableName, nodeId, string.Empty);
                var deleteNum = BaiRongDataProvider.ContentDao.DeleteContentsByNodeId(publishmentSystemId, tableName, nodeId, contentIdList);

                if (nodeId > 0 && deleteNum > 0)
                {
                    new Action(() =>
                    {
                        DataProvider.NodeDao.UpdateContentNum(PublishmentSystemManager.GetPublishmentSystemInfo(publishmentSystemId), nodeId, true);
                    }).BeginInvoke(null, null);
                }

                ClearStlCache();
            }
        }

        public void DeletePreviewContents(int publishmentSystemId, string tableName, NodeInfo nodeInfo)
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                nodeInfo.Additional.IsPreviewContents = false;
                DataProvider.NodeDao.UpdateAdditional(nodeInfo);

                string sqlString =
                    $"DELETE FROM {tableName} WHERE PublishmentSystemId = {publishmentSystemId} AND NodeId = {nodeInfo.NodeId} AND SourceId = {SourceManager.Preview}";
                BaiRongDataProvider.DatabaseDao.ExecuteSql(sqlString);
            }
        }

        public void RestoreContentsByTrash(int publishmentSystemId, string tableName)
        {
            var updateNum = BaiRongDataProvider.ContentDao.RestoreContentsByTrash(publishmentSystemId, tableName);
            if (updateNum > 0)
            {
                new Action(() =>
                {
                    DataProvider.NodeDao.UpdateContentNum(PublishmentSystemManager.GetPublishmentSystemInfo(publishmentSystemId));
                }).BeginInvoke(null, null);
            }
        }

        public string GetWhereStringByStlSearch(bool isAllSites, string siteName, string siteDir, string siteIds, string channelIndex, string channelName, string channelIds, string type, string word, string dateAttribute, string dateFrom, string dateTo, string since, int publishmentSystemId, List<string> excludeAttributes, NameValueCollection form, out bool isDefaultCondition)
        {
            isDefaultCondition = true;
            var whereBuilder = new StringBuilder();

            PublishmentSystemInfo publishmentSystemInfo = null;
            if (!string.IsNullOrEmpty(siteName))
            {
                publishmentSystemInfo = PublishmentSystemManager.GetPublishmentSystemInfoBySiteName(siteName);
            }
            else if (!string.IsNullOrEmpty(siteDir))
            {
                publishmentSystemInfo = PublishmentSystemManager.GetPublishmentSystemInfoByDirectory(siteDir);
            }
            if (publishmentSystemInfo == null)
            {
                publishmentSystemInfo = PublishmentSystemManager.GetPublishmentSystemInfo(publishmentSystemId);
            }

            var channelId = DataProvider.NodeDao.GetNodeIdByChannelIdOrChannelIndexOrChannelName(publishmentSystemId, publishmentSystemId, channelIndex, channelName);
            var nodeInfo = NodeManager.GetNodeInfo(publishmentSystemId, channelId);

            if (isAllSites)
            {
                whereBuilder.Append("(PublishmentSystemId > 0) ");
            }
            else if (!string.IsNullOrEmpty(siteIds))
            {
                whereBuilder.Append($"(PublishmentSystemId IN ({TranslateUtils.ToSqlInStringWithoutQuote(TranslateUtils.StringCollectionToIntList(siteIds))})) ");
            }
            else
            {
                whereBuilder.Append($"(PublishmentSystemId = {publishmentSystemInfo.PublishmentSystemId}) ");
            }

            if (!string.IsNullOrEmpty(channelIds))
            {
                whereBuilder.Append(" AND ");
                var nodeIdList = new List<int>();
                foreach (var nodeId in TranslateUtils.StringCollectionToIntList(channelIds))
                {
                    nodeIdList.Add(nodeId);
                    nodeIdList.AddRange(DataProvider.NodeDao.GetNodeIdListForDescendant(nodeId));
                }
                whereBuilder.Append(nodeIdList.Count == 1
                    ? $"(NodeId = {nodeIdList[0]}) "
                    : $"(NodeId IN ({TranslateUtils.ToSqlInStringWithoutQuote(nodeIdList)})) ");
            }
            else if (channelId != publishmentSystemId)
            {
                whereBuilder.Append(" AND ");
                var nodeIdList = DataProvider.NodeDao.GetNodeIdListForDescendant(channelId);
                nodeIdList.Add(channelId);
                whereBuilder.Append(nodeIdList.Count == 1
                    ? $"(NodeId = {nodeIdList[0]}) "
                    : $"(NodeId IN ({TranslateUtils.ToSqlInStringWithoutQuote(nodeIdList)})) ");
            }

            var typeList = new List<string>();
            if (string.IsNullOrEmpty(type))
            {
                typeList.Add(ContentAttribute.Title);
            }
            else
            {
                typeList = TranslateUtils.StringCollectionToStringList(type);
            }

            if (!string.IsNullOrEmpty(word))
            {
                whereBuilder.Append(" AND (");
                foreach (var attributeName in typeList)
                {
                    whereBuilder.Append($"[{attributeName}] LIKE '%{PageUtils.FilterSql(word)}%' OR ");
                }
                whereBuilder.Length = whereBuilder.Length - 3;
                whereBuilder.Append(")");
            }

            if (string.IsNullOrEmpty(dateAttribute))
            {
                dateAttribute = ContentAttribute.AddDate;
            }

            if (!string.IsNullOrEmpty(dateFrom))
            {
                whereBuilder.Append(" AND ");
                whereBuilder.Append($" {dateAttribute} >= {SqlUtils.GetComparableDate(TranslateUtils.ToDateTime(dateFrom))} ");
            }
            if (!string.IsNullOrEmpty(dateTo))
            {
                whereBuilder.Append(" AND ");
                whereBuilder.Append($" {dateAttribute} <= {SqlUtils.GetComparableDate(TranslateUtils.ToDateTime(dateTo))} ");
            }
            if (!string.IsNullOrEmpty(since))
            {
                var sinceDate = DateTime.Now.AddHours(-DateUtils.GetSinceHours(since));
                whereBuilder.Append($" AND {dateAttribute} BETWEEN {SqlUtils.GetComparableDateTime(sinceDate)} AND {SqlUtils.GetComparableNow()} ");
            }

            var tableName = NodeManager.GetTableName(publishmentSystemInfo, nodeInfo);
            var styleInfoList = RelatedIdentities.GetTableStyleInfoList(publishmentSystemInfo, nodeInfo.NodeId);

            foreach (string key in form.Keys)
            {
                if (excludeAttributes.Contains(key.ToLower())) continue;
                if (string.IsNullOrEmpty(form[key])) continue;

                var value = StringUtils.Trim(form[key]);
                if (string.IsNullOrEmpty(value)) continue;

                if (TableMetadataManager.IsAttributeNameExists(tableName, key))
                {
                    whereBuilder.Append(" AND ");
                    whereBuilder.Append($"({key} LIKE '%{value}%')");
                }
                else
                {
                    foreach (var tableStyleInfo in styleInfoList)
                    {
                        if (StringUtils.EqualsIgnoreCase(tableStyleInfo.AttributeName, key))
                        {
                            whereBuilder.Append(" AND ");
                            whereBuilder.Append($"({ContentAttribute.SettingsXml} LIKE '%{key}={value}%')");
                            break;
                        }
                    }
                }
            }

            if (whereBuilder.ToString().Contains(" AND "))
            {
                isDefaultCondition = false;
            }

            return whereBuilder.ToString();
        }

        public string GetSelectCommend(string tableName, int publishmentSystemId, int nodeId, bool isSystemAdministrator, List<int> owningNodeIdList, string searchType, string keyword, string dateFrom, string dateTo, bool isSearchChildren, ETriState checkedState)
        {
            return GetSelectCommend(tableName, publishmentSystemId, nodeId, isSystemAdministrator, owningNodeIdList, searchType, keyword, dateFrom, dateTo, isSearchChildren, checkedState, false, false);
        }

        public string GetSelectCommend(string tableName, int publishmentSystemId, int nodeId, bool isSystemAdministrator, List<int> owningNodeIdList, string searchType, string keyword, string dateFrom, string dateTo, bool isSearchChildren, ETriState checkedState, bool isNoDup, bool isTrashContent)
        {
            var nodeInfo = NodeManager.GetNodeInfo(publishmentSystemId, nodeId);
            var nodeIdList = DataProvider.NodeDao.GetNodeIdListByScopeType(nodeInfo.NodeId, nodeInfo.ChildrenCount,
                isSearchChildren ? EScopeType.All : EScopeType.Self, string.Empty, string.Empty, nodeInfo.ContentModelPluginId);

            var list = new List<int>();
            if (isSystemAdministrator)
            {
                list = nodeIdList;
            }
            else
            {
                foreach (int theNodeId in nodeIdList)
                {
                    if (owningNodeIdList.Contains(theNodeId))
                    {
                        list.Add(theNodeId);
                    }
                }
            }

            return BaiRongDataProvider.ContentDao.GetSelectCommendByCondition(tableName, publishmentSystemId, list, searchType, keyword, dateFrom, dateTo, checkedState, isNoDup, isTrashContent);
        }

        public string GetSelectCommend(string tableName, int publishmentSystemId, int nodeId, bool isSystemAdministrator, List<int> owningNodeIdList, string searchType, string keyword, string dateFrom, string dateTo, bool isSearchChildren, ETriState checkedState, bool isNoDup, bool isTrashContent, bool isWritingOnly, string userNameOnly)
        {
            var nodeInfo = NodeManager.GetNodeInfo(publishmentSystemId, nodeId);
            var nodeIdList = DataProvider.NodeDao.GetNodeIdListByScopeType(nodeInfo.NodeId, nodeInfo.ChildrenCount, isSearchChildren ? EScopeType.All : EScopeType.Self, string.Empty, string.Empty, nodeInfo.ContentModelPluginId);

            var list = new List<int>();
            if (isSystemAdministrator)
            {
                list = nodeIdList;
            }
            else
            {
                foreach (int theNodeId in nodeIdList)
                {
                    if (owningNodeIdList.Contains(theNodeId))
                    {
                        list.Add(theNodeId);
                    }
                }
            }

            return BaiRongDataProvider.ContentDao.GetSelectCommendByCondition(tableName, publishmentSystemId, list, searchType, keyword, dateFrom, dateTo, checkedState, isNoDup, isTrashContent, isWritingOnly, userNameOnly);
        }

        public string GetWritingSelectCommend(string writingUserName, string tableName, int publishmentSystemId, List<int> nodeIdList, string searchType, string keyword, string dateFrom, string dateTo)
        {
            if (nodeIdList == null || nodeIdList.Count == 0)
            {
                return null;
            }

            var whereString = new StringBuilder($"WHERE WritingUserName = '{writingUserName}' ");

            if (nodeIdList.Count == 1)
            {
                whereString.AppendFormat("AND PublishmentSystemId = {0} AND NodeId = {1} ", publishmentSystemId, nodeIdList[0]);
            }
            else
            {
                whereString.AppendFormat("AND PublishmentSystemId = {0} AND NodeId IN ({1}) ", publishmentSystemId, TranslateUtils.ToSqlInStringWithoutQuote(nodeIdList));
            }

            var dateString = string.Empty;
            if (!string.IsNullOrEmpty(dateFrom))
            {
                dateString = $" AND AddDate >= {SqlUtils.GetComparableDate(TranslateUtils.ToDateTime(dateFrom))} ";
            }
            if (!string.IsNullOrEmpty(dateTo))
            {
                dateString += $" AND AddDate <= {SqlUtils.GetComparableDate(TranslateUtils.ToDateTime(dateTo).AddDays(1))} ";
            }

            if (string.IsNullOrEmpty(keyword))
            {
                whereString.Append(dateString);
            }
            else
            {
                var list = TableMetadataManager.GetAllLowerAttributeNameList(tableName);
                if (list.Contains(searchType.ToLower()))
                {
                    whereString.AppendFormat("AND ([{0}] LIKE '%{1}%') {2} ", searchType, keyword, dateString);
                }
            }

            return BaiRongDataProvider.DatabaseDao.GetSelectSqlString(tableName, SqlUtils.Asterisk, whereString.ToString());
        }

        public string GetSelectCommendByContentGroup(string tableName, string contentGroupName, int publishmentSystemId)
        {
            contentGroupName = PageUtils.FilterSql(contentGroupName);
            string sqlString =
                $"SELECT * FROM {tableName} WHERE PublishmentSystemId = {publishmentSystemId} AND NodeId > 0 AND (ContentGroupNameCollection LIKE '{contentGroupName},%' OR ContentGroupNameCollection LIKE '%,{contentGroupName}' OR ContentGroupNameCollection  LIKE '%,{contentGroupName},%'  OR ContentGroupNameCollection='{contentGroupName}')";
            return sqlString;
        }

        public DataSet GetStlDataSourceChecked(List<int> nodeIdList, string tableName, int startNum, int totalNum, string orderByString, string whereString, bool isNoDup, LowerNameValueCollection others)
        {
            return BaiRongDataProvider.ContentDao.GetStlDataSourceChecked(tableName, nodeIdList, startNum, totalNum, orderByString, whereString, isNoDup, others);
        }

        public string GetStlSqlStringChecked(List<int> nodeIdList, string tableName, int publishmentSystemId, int nodeId, int startNum, int totalNum, string orderByString, string whereString, EScopeType scopeType, string groupChannel, string groupChannelNot, bool isNoDup)
        {
            string sqlWhereString;

            if (publishmentSystemId == nodeId && scopeType == EScopeType.All && string.IsNullOrEmpty(groupChannel) && string.IsNullOrEmpty(groupChannelNot))
            {
                sqlWhereString =
                    $"WHERE (PublishmentSystemId = {publishmentSystemId} AND NodeId > 0 AND IsChecked = '{true}' {whereString})";
            }
            else
            {
                if (nodeIdList == null || nodeIdList.Count == 0)
                {
                    return string.Empty;
                }
                sqlWhereString = nodeIdList.Count == 1 ? $"WHERE (NodeId = {nodeIdList[0]} AND IsChecked = '{true}' {whereString})" : $"WHERE (NodeId IN ({TranslateUtils.ToSqlInStringWithoutQuote(nodeIdList)}) AND IsChecked = '{true}' {whereString})";
            }

            if (isNoDup)
            {
                var sqlString = BaiRongDataProvider.DatabaseDao.GetSelectSqlString(tableName, "MIN(Id)", sqlWhereString + " GROUP BY Title");
                sqlWhereString += $" AND Id IN ({sqlString})";
            }

            if (!string.IsNullOrEmpty(tableName))
            {
                return BaiRongDataProvider.DatabaseDao.GetSelectSqlString(tableName, startNum, totalNum, BaiRongDataProvider.ContentDao.StlColumns, sqlWhereString, orderByString);
            }
            return string.Empty;
        }

        public string GetStlSqlStringCheckedBySearch(string tableName, int startNum, int totalNum, string orderByString, string whereString, bool isNoDup)
        {
            string sqlWhereString =
                    $"WHERE (NodeId > 0 AND IsChecked = '{true}' {whereString})";
            if (isNoDup)
            {
                var sqlString = BaiRongDataProvider.DatabaseDao.GetSelectSqlString(tableName, "MIN(Id)", sqlWhereString + " GROUP BY Title");
                sqlWhereString += $" AND Id IN ({sqlString})";
            }

            if (!string.IsNullOrEmpty(tableName))
            {
                return BaiRongDataProvider.DatabaseDao.GetSelectSqlString(tableName, startNum, totalNum, $"{ContentAttribute.Id}, {ContentAttribute.NodeId}, {ContentAttribute.IsTop}, {ContentAttribute.AddDate}", sqlWhereString, orderByString);
            }
            return string.Empty;
        }

        public void TidyUp(string tableName, int nodeId, string attributeName, bool isDesc)
        {
            var taxisDirection = isDesc ? "ASC" : "DESC";//升序,但由于页面排序是按Taxis的Desc排序的，所以这里sql里面的ASC/DESC取反

            string sqlString =
                $"SELECT Id, IsTop FROM {tableName} WHERE NodeId = {nodeId} OR NodeId = -{nodeId} ORDER BY {attributeName} {taxisDirection}";
            var sqlList = new List<string>();

            using (var rdr = ExecuteReader(sqlString))
            {
                var taxis = 1;
                while (rdr.Read())
                {
                    var id = GetInt(rdr, 0);
                    var isTop = GetBool(rdr, 1);

                    sqlList.Add(
                        $"UPDATE {tableName} SET Taxis = {taxis++}, IsTop = '{isTop}' WHERE Id = {id}");
                }
                rdr.Close();
            }

            BaiRongDataProvider.DatabaseDao.ExecuteSql(sqlList);
        }

        public List<int> GetIdListBySameTitleInOneNode(string tableName, int nodeId, string title)
        {
            var list = new List<int>();
            string sql = $"SELECT Id FROM {tableName} WHERE NodeId = {nodeId} AND Title = '{title}'";
            using (var rdr = ExecuteReader(sql))
            {
                while (rdr.Read())
                {
                    list.Add(GetInt(rdr, 0));
                }
                rdr.Close();
            }
            return list;
        }

        public List<IContentInfo> GetListByLimitAndOffset(string tableName, int nodeId, string whereString, string orderString, int limit, int offset)
        {
            var list = new List<IContentInfo>();
            if (!string.IsNullOrEmpty(whereString))
            {
                whereString = whereString.Replace("WHERE ", string.Empty).Replace("where ", string.Empty);
            }
            if (!string.IsNullOrEmpty(orderString))
            {
                orderString = orderString.Replace("ORDER BY ", string.Empty).Replace("order by ", string.Empty);
            }
            var firstWhere = string.IsNullOrEmpty(whereString) ? string.Empty : $"WHERE {whereString}";
            var secondWhere = string.IsNullOrEmpty(whereString) ? string.Empty : $"AND {whereString}";
            var order = string.IsNullOrEmpty(orderString) ? "IsTop DESC, Id DESC" : orderString;

            var sqlString = $"SELECT * FROM {tableName} {firstWhere} ORDER BY {order}";
            if (limit > 0 && offset > 0)
            {
                switch (WebConfigUtils.DatabaseType)
                {
                    case EDatabaseType.MySql:
                        sqlString = $"SELECT * FROM {tableName} {firstWhere} ORDER BY {order} limit {limit} offset {offset}";
                        break;
                    case EDatabaseType.SqlServer:
                        sqlString = $@"SELECT TOP {limit} * FROM {tableName} WHERE Id NOT IN (SELECT TOP {offset} Id FROM {tableName} {firstWhere} ORDER BY {order}) {secondWhere} ORDER BY {order}";
                        break;
                    case EDatabaseType.PostgreSql:
                        sqlString = $"SELECT * FROM {tableName} {firstWhere} ORDER BY {order} limit {limit} offset {offset}";
                        break;
                    case EDatabaseType.Oracle:
                        sqlString = $"SELECT * FROM {tableName} {firstWhere} ORDER BY {order} OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else if (limit > 0)
            {
                switch (WebConfigUtils.DatabaseType)
                {
                    case EDatabaseType.MySql:
                        sqlString = $"SELECT * FROM {tableName} {firstWhere} ORDER BY {order} limit {limit}";
                        break;
                    case EDatabaseType.SqlServer:
                        sqlString = $@"SELECT TOP {limit} * FROM {tableName} {firstWhere} ORDER BY {order}";
                        break;
                    case EDatabaseType.PostgreSql:
                        sqlString = $"SELECT * FROM {tableName} {firstWhere} ORDER BY {order} limit {limit}";
                        break;
                    case EDatabaseType.Oracle:
                        sqlString = $"SELECT * FROM {tableName} {firstWhere} ORDER BY {order} FETCH FIRST {limit} ROWS ONLY";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else if (offset > 0)
            {
                switch (WebConfigUtils.DatabaseType)
                {
                    case EDatabaseType.MySql:
                        sqlString = $"SELECT * FROM {tableName} {firstWhere} ORDER BY {order} offset {offset}";
                        break;
                    case EDatabaseType.SqlServer:
                        sqlString =
                            $@"SELECT * FROM {tableName} WHERE Id NOT IN (SELECT TOP {offset} Id FROM {tableName} {firstWhere} ORDER BY {order}) {secondWhere} ORDER BY {order}";
                        break;
                    case EDatabaseType.PostgreSql:
                        sqlString = $"SELECT * FROM {tableName} {firstWhere} ORDER BY {order} offset {offset}";
                        break;
                    case EDatabaseType.Oracle:
                        sqlString = $"SELECT * FROM {tableName} {firstWhere} ORDER BY {order} OFFSET {offset} ROWS";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            using (var rdr = ExecuteReader(sqlString))
            {
                while (rdr.Read())
                {
                    var info = GetContentInfo(rdr);
                    list.Add(info);
                }
                rdr.Close();
            }

            return list;
        }

        public int GetCount(string tableName, int nodeId, string whereString)
        {
            if (!string.IsNullOrEmpty(whereString))
            {
                whereString = whereString.Replace("WHERE ", string.Empty).Replace("where ", string.Empty);
            }
            whereString = string.IsNullOrEmpty(whereString) ? string.Empty : $"WHERE {whereString}";

            string sqlString = $"SELECT COUNT(*) FROM {tableName} {whereString}";

            return BaiRongDataProvider.DatabaseDao.GetIntResult(sqlString);
        }
    }
}
