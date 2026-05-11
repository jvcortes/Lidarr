import { createAction } from 'redux-actions';
import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import createSaveHandler from 'Store/Actions/Creators/createSaveHandler';
import createSetSettingValueReducer from 'Store/Actions/Creators/Reducers/createSetSettingValueReducer';
import { createThunk } from 'Store/thunks';

//
// Variables

const section = 'settings.coverArtProviders';

//
// Action Types

export const FETCH_COVER_ART_PROVIDER_SETTINGS = 'settings/coverArtProviders/fetchCoverArtProviderSettings';
export const SET_COVER_ART_PROVIDER_SETTINGS_VALUE = 'settings/coverArtProviders/setCoverArtProviderSettingsValue';
export const SAVE_COVER_ART_PROVIDER_SETTINGS = 'settings/coverArtProviders/saveCoverArtProviderSettings';

//
// Action Creators

export const fetchCoverArtProviderSettings = createThunk(FETCH_COVER_ART_PROVIDER_SETTINGS);
export const saveCoverArtProviderSettings = createThunk(SAVE_COVER_ART_PROVIDER_SETTINGS);
export const setCoverArtProviderSettingsValue = createAction(SET_COVER_ART_PROVIDER_SETTINGS_VALUE, (payload) => {
  return {
    section,
    ...payload
  };
});

//
// Details

export default {

  //
  // State

  defaultState: {
    isFetching: false,
    isPopulated: false,
    error: null,
    pendingChanges: {},
    isSaving: false,
    saveError: null,
    item: {}
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_COVER_ART_PROVIDER_SETTINGS]: createFetchHandler(section, '/config/coverartproviders'),
    [SAVE_COVER_ART_PROVIDER_SETTINGS]: createSaveHandler(section, '/config/coverartproviders')
  },

  //
  // Reducers

  reducers: {
    [SET_COVER_ART_PROVIDER_SETTINGS_VALUE]: createSetSettingValueReducer(section)
  }

};
