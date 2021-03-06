﻿
module vdb.viewModels.songs {
	
	import cls = vdb.models;
	import dc = vdb.dataContracts;

	export class PlayListRepositoryForSongListAdapter implements IPlayListRepository {

		constructor(private songListRepo: rep.SongListRepository, private songListId: number) { }

		public getSongs = (
			pvServices: string,
			paging: dc.PagingProperties,
			fields: cls.SongOptionalFields,
			lang: cls.globalization.ContentLanguagePreference,
			callback: (result: dc.PartialFindResultContract<ISongForPlayList>) => void) => {

			this.songListRepo.getSongs(this.songListId, pvServices, paging, fields, lang, result => {

				var mapped = _.map(result.items, (song, idx) => {
					return {
						name: song.order + ". " + song.song.name + (song.notes ? " (" + song.notes + ")" : ""),
						song: song.song,
						indexInPlayList: paging.start + idx
					}
				});

				callback({ items: mapped, totalCount: result.totalCount });

			});

		}

	}

}