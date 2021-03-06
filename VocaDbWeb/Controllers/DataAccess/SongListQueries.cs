﻿using System;
using System.Linq;
using NLog;
using VocaDb.Model;
using VocaDb.Model.DataContracts;
using VocaDb.Model.DataContracts.Songs;
using VocaDb.Model.Domain;
using VocaDb.Model.Domain.Activityfeed;
using VocaDb.Model.Domain.Images;
using VocaDb.Model.Domain.Security;
using VocaDb.Model.Domain.Songs;
using VocaDb.Model.Domain.Users;
using VocaDb.Model.Service;
using VocaDb.Model.Service.QueryableExtenders;
using VocaDb.Model.Service.Repositories;
using VocaDb.Model.Service.Search.SongSearch;

namespace VocaDb.Web.Controllers.DataAccess {

	public class SongListQueries {

		private readonly IEntryLinkFactory entryLinkFactory;
		private static readonly Logger log = LogManager.GetCurrentClassLogger();
		private readonly IEntryImagePersisterOld imagePersister;
		private readonly IUserPermissionContext permissionContext;
		private readonly ISongListRepository repository;

		private IUserPermissionContext PermissionContext {
			get { return permissionContext; }
		}

		public void Archive(IRepositoryContext<SongList> ctx, SongList songList, SongListDiff diff, EntryEditEvent reason) {

			var agentLoginData = ctx.CreateAgentLoginData(PermissionContext);
			var archived = songList.CreateArchivedVersion(diff, agentLoginData, reason);
			ctx.OfType<ArchivedSongListVersion>().Save(archived);

		}

		private User GetLoggedUser(IRepositoryContext<SongList> ctx) {

			permissionContext.VerifyLogin();

			return ctx.OfType<User>().Load(permissionContext.LoggedUser.Id);

		}

		private PartialFindResult<T> GetSongsInList<T>(IRepositoryContext<SongList> session, SongListQueryParams queryParams,
			Func<SongInList, T> fac) {

			var q = session.OfType<SongInList>().Query()
				.Where(a => !a.Song.Deleted && a.List.Id == queryParams.ListId)
				.WhereSongHasPVService(queryParams.PVServices);

			IQueryable<SongInList> resultQ = q.OrderBy(s => s.Order);
			resultQ = resultQ.Skip(queryParams.Paging.Start).Take(queryParams.Paging.MaxEntries);

			var contracts = resultQ.ToArray().Select(s => fac(s)).ToArray();
			var totalCount = (queryParams.Paging.GetTotalCount ? q.Count() : 0);

			return new PartialFindResult<T>(contracts, totalCount);

		}

		private SongList CreateSongList(IRepositoryContext<SongList> ctx, SongListForEditContract contract, UploadedFileContract uploadedFile) {

			var user = GetLoggedUser(ctx);
			var newList = new SongList(contract.Name, user);
			newList.Description = contract.Description;

			if (EntryPermissionManager.CanManageFeaturedLists(permissionContext))
				newList.FeaturedCategory = contract.FeaturedCategory;

			ctx.Save(newList);

			var songDiff = newList.SyncSongs(contract.SongLinks, c => ctx.OfType<Song>().Load(c.Song.Id));
			ctx.OfType<SongInList>().Sync(songDiff);

			SetThumb(newList, uploadedFile);

			ctx.Update(newList);

			ctx.AuditLogger.AuditLog(string.Format("created song list {0}", entryLinkFactory.CreateEntryLink(newList)), user);
			Archive(ctx, newList, new SongListDiff(), EntryEditEvent.Created);

			return newList;

		}

		private void SetThumb(SongList list, UploadedFileContract uploadedFile) {

			if (uploadedFile != null) {

				var thumb = new EntryThumb(list, uploadedFile.Mime);
				list.Thumb = thumb;
				var thumbGenerator = new ImageThumbGenerator(imagePersister);
				thumbGenerator.GenerateThumbsAndMoveImage(uploadedFile.Stream, thumb, ImageSizes.Original | ImageSizes.SmallThumb, originalSize: 500);

			}

		}

		public SongListQueries(ISongListRepository repository, IUserPermissionContext permissionContext, IEntryLinkFactory entryLinkFactory, IEntryImagePersisterOld imagePersister) {
			this.repository = repository;
			this.permissionContext = permissionContext;
			this.entryLinkFactory = entryLinkFactory;
			this.imagePersister = imagePersister;
		}

		public PartialFindResult<SongInListContract> GetSongsInList(SongListQueryParams queryParams) {

			return repository.HandleQuery(session => GetSongsInList(session, queryParams, s => new SongInListContract(s, PermissionContext.LanguagePreference)));

		}

		public PartialFindResult<T> GetSongsInList<T>(SongListQueryParams queryParams, Func<SongInList, T> fac) {

			return repository.HandleQuery(ctx => GetSongsInList(ctx, queryParams, fac));

		}

		public SongListContract GetSongList(int listId) {

			return repository.HandleQuery(session => new SongListContract(session.Load(listId), PermissionContext));
		
		}

		public SongListForEditContract GetSongListForEdit(int listId) {

			return repository.HandleQuery(session => new SongListForEditContract(session.Load(listId), PermissionContext));

		}

		public SongListWithArchivedVersionsContract GetSongListWithArchivedVersions(int id) {

			return repository.HandleQuery(session => new SongListWithArchivedVersionsContract(session.Load(id), PermissionContext));

		}

		public int UpdateSongList(SongListForEditContract contract, UploadedFileContract uploadedFile) {

			ParamIs.NotNull(() => contract);

			PermissionContext.VerifyPermission(PermissionToken.EditProfile);

			return repository.HandleTransaction(ctx => {

				var user = GetLoggedUser(ctx);
				SongList list;

				if (contract.Id == 0) {

					list = CreateSongList(ctx, contract, uploadedFile);

				} else {

					list = ctx.Load(contract.Id);
					var diff = new SongListDiff();

					EntryPermissionManager.VerifyEdit(PermissionContext, list);

					if (list.Description != contract.Description) {
						diff.Description.Set();
						list.Description = contract.Description;						
					}

					if (list.Name != contract.Name) {
						diff.Name.Set();
						list.Name = contract.Name;						
					}

					if (EntryPermissionManager.CanManageFeaturedLists(PermissionContext) && list.FeaturedCategory != contract.FeaturedCategory) {
						diff.FeaturedCategory.Set();
						list.FeaturedCategory = contract.FeaturedCategory;						
					}

					var songDiff = list.SyncSongs(contract.SongLinks, c => ctx.OfType<Song>().Load(c.Song.Id));

					if (songDiff.Changed) {
						diff.Songs.Set();
					}

					ctx.OfType<SongInList>().Sync(songDiff);

					if (uploadedFile != null) {
						diff.Thumbnail.Set();
						SetThumb(list, uploadedFile);						
					}

					ctx.Update(list);

					ctx.AuditLogger.AuditLog(string.Format("updated song list {0}", entryLinkFactory.CreateEntryLink(list)), user);
					Archive(ctx, list, diff, EntryEditEvent.Updated);

				}

				return list.Id;

			});

		}

	}

}