import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import { set, updateItem } from './baseActions';
import createHandleActions from './Creators/createHandleActions';

//
// Variables

export const section = 'coverArt';

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  isSaving: false,
  saveError: null,
  items: []
};

//
// Action Types

export const FETCH_COVER_ART_CANDIDATES = 'coverArt/fetchCoverArtCandidates';
export const SELECT_COVER_ART = 'coverArt/selectCoverArt';
export const RESET_COVER_ART = 'coverArt/resetCoverArt';
export const CLEAR_COVER_ART_CANDIDATES = 'coverArt/clearCoverArtCandidates';

//
// Action Creators

export const fetchCoverArtCandidates = createThunk(FETCH_COVER_ART_CANDIDATES);
export const selectCoverArt = createThunk(SELECT_COVER_ART);
export const resetCoverArt = createThunk(RESET_COVER_ART);
export const clearCoverArtCandidates = createAction(CLEAR_COVER_ART_CANDIDATES);

//
// Action Handlers

export const actionHandlers = handleThunks({

  [FETCH_COVER_ART_CANDIDATES]: function(getState, payload, dispatch) {
    const { albumId } = payload;

    dispatch(set({ section, isFetching: true, isPopulated: false, error: null }));

    const { request } = createAjaxRequest({
      url: `/album/${albumId}/coverartcandidates`,
      method: 'GET',
      dataType: 'json'
    });

    request.done((data) => {
      dispatch(batchActions([
        set({
          section,
          isFetching: false,
          isPopulated: true,
          error: null,
          items: data
        })
      ]));
    });

    request.fail((xhr) => {
      dispatch(set({
        section,
        isFetching: false,
        isPopulated: false,
        error: xhr.aborted ? null : xhr
      }));
    });
  },

  [SELECT_COVER_ART]: function(getState, payload, dispatch) {
    const { albumId, coverUrl } = payload;

    dispatch(set({ section, isSaving: true, saveError: null }));

    const { request } = createAjaxRequest({
      url: `/album/${albumId}/coverart`,
      method: 'PUT',
      contentType: 'application/json',
      dataType: 'json',
      data: JSON.stringify({ coverUrl })
    });

    request.done((data) => {
      dispatch(batchActions([
        set({ section, isSaving: false, saveError: null }),
        updateItem({ section: 'albums', ...data })
      ]));
    });

    request.fail((xhr) => {
      dispatch(set({
        section,
        isSaving: false,
        saveError: xhr.aborted ? null : xhr
      }));
    });
  },

  [RESET_COVER_ART]: function(getState, payload, dispatch) {
    const { albumId } = payload;

    dispatch(set({ section, isSaving: true, saveError: null }));

    const { request } = createAjaxRequest({
      url: `/album/${albumId}/coverart`,
      method: 'DELETE',
      dataType: 'json'
    });

    request.done((data) => {
      dispatch(batchActions([
        set({ section, isSaving: false, saveError: null }),
        updateItem({ section: 'albums', ...data })
      ]));
    });

    request.fail((xhr) => {
      dispatch(set({
        section,
        isSaving: false,
        saveError: xhr.aborted ? null : xhr
      }));
    });
  }

});

//
// Reducers

export const reducers = createHandleActions({

  [CLEAR_COVER_ART_CANDIDATES]: (state) => {
    return Object.assign({}, state, defaultState);
  }

}, defaultState, section);
