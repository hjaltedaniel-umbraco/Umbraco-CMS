﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Umbraco.Core.IO;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Persistence.Repositories
{
    /// <summary>
    /// Represents the Stylesheet Repository
    /// </summary>
    internal class StylesheetRepository : FileRepository<string, Stylesheet>, IStylesheetRepository
    {
        private readonly IDatabaseUnitOfWork _dbwork;

        internal StylesheetRepository(IUnitOfWork work, IDatabaseUnitOfWork db, IFileSystem fileSystem)
			: base(work, fileSystem)
	    {
            _dbwork = db;
	    }

        public StylesheetRepository(IUnitOfWork work, IDatabaseUnitOfWork db)
            : this(work, db, new PhysicalFileSystem(SystemDirectories.Css))
        {
        }

        #region Overrides of FileRepository<string,Stylesheet>

        public override Stylesheet Get(string id)
        {
            if (!FileSystem.FileExists(id))
            {
                throw new Exception(string.Format("The file {0} was not found", id));
            }

            var content = string.Empty;

            using (var stream = FileSystem.OpenFile(id))
            {
                byte[] bytes = new byte[stream.Length];
                stream.Position = 0;
                stream.Read(bytes, 0, (int) stream.Length);
                content = Encoding.UTF8.GetString(bytes);
            }

            var path = FileSystem.GetRelativePath(id);
            var created = FileSystem.GetCreated(path).UtcDateTime;
            var updated = FileSystem.GetLastModified(path).UtcDateTime;

            var stylesheet = new Stylesheet(path)
                                 {
                                     Content = content,
                                     Key = path.EncodeAsGuid(),
                                     CreateDate = created,
                                     UpdateDate = updated,
                                     Id = GetStylesheetId(path)
                                 };

            //on initial construction we don't want to have dirty properties tracked
            // http://issues.umbraco.org/issue/U4-1946
            stylesheet.ResetDirtyProperties(false);

            return stylesheet;
            
        }

        // Fix for missing Id's on FileService.GetStylesheets() call.  This is needed as sytlesheets can only bo loaded in the editor via 
        //  their Id so listing stylesheets needs to list there Id as well for custom plugins to render the build in editor.
        //  http://issues.umbraco.org/issue/U4-3258
        private int GetStylesheetId(string path)
        {
            var sql = new Sql()
                .Select("nodeId")
                .From("umbracoNode")
                .Where("umbracoNode.nodeObjectType = @NodeObjectType && umbracoNode.text = @Alias",
                    new { NodeObjectType = UmbracoObjectTypes.Stylesheet, Alias = path.TrimEnd(".css").Replace("\\", "/") });
            var nodeDto = _dbwork.Database.FirstOrDefault<Umbraco.Core.Models.Rdbms.NodeDto>(sql);
            return nodeDto == null ? 0 : nodeDto.NodeId;

            //var ss = ApplicationContext.Current.Services.EntityService.GetRootEntities(UmbracoObjectTypes.Stylesheet).SingleOrDefault(s => s.Name == path.TrimEnd(".css").Replace("\\", "/"));
            //return ss == null ? 0 : ss.Id;
        }

        public override IEnumerable<Stylesheet> GetAll(params string[] ids)
        {
            if (ids.Any())
            {
                foreach (var id in ids)
                {
                    yield return Get(id);
                }
            }
            else
            {
                var files = FindAllFiles("");
                foreach (var file in files)
                {
                    yield return Get(file);
                }
            }
        }

        #endregion
    }
}